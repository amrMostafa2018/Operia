using MediatR;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.Application.Employees.Common;
using Operia.Domain.Entities;

namespace Operia.Application.Employees.Commands.CreateEmployee;

public sealed class CreateEmployeeHandler : IRequestHandler<CreateEmployeeCommand, EmployeeDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmployeeCodeGenerator _employeeCodeGenerator;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _files;

    public CreateEmployeeHandler(
        IApplicationDbContext db,
        IEmployeeCodeGenerator employeeCodeGenerator,
        IIdentityService identityService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService files)
    {
        _db = db;
        _employeeCodeGenerator = employeeCodeGenerator;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _files = files;
    }

    public async Task<EmployeeDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = EmployeeHandlerHelpers.RequireTenant(_currentUserService);
        EmployeeHandlerHelpers.ValidateRole(request.Role);
        await EmployeeHandlerHelpers.ValidateBranchesAsync(_db, tenantId, request.BranchIds, cancellationToken);
        await _identityService.EnsureIdentityUniqueAsync(null, request.UserName, request.Email, request.MobileNumber, cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        string? photoUrl = null;
        try
        {
            if (request.Photo is not null)
                photoUrl = await EmployeeHandlerHelpers.SavePhotoAsync(_files, tenantId, request.Photo, cancellationToken);

            var identityUserId = await _identityService.CreateUserAsync(
                request.FullName,
                request.UserName,
                request.Email,
                request.MobileNumber,
                request.Role,
                request.TemporaryPassword,
                tenantId,
                cancellationToken);

            var code = await _employeeCodeGenerator.ReserveNextAsync(tenantId, cancellationToken);
            var employee = new Employee
            {
                TenantId = tenantId,
                IdentityUserId = identityUserId,
                Code = code,
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                MobileNumber = PhoneNumberHelper.ToE164(request.MobileNumber),
                Specialty = EmployeeHandlerHelpers.Clean(request.Specialty),
                JobTitle = EmployeeHandlerHelpers.Clean(request.JobTitle),
                JoiningDate = request.JoiningDate,
                PhotoUrl = photoUrl,
                IsActive = request.IsActive
            };

            EmployeeHandlerHelpers.AddBranches(employee, request.BranchIds);
            _db.Employees.Add(employee);

            if (!request.IsActive)
            {
                await _identityService.SetStatusAsync(identityUserId, false, cancellationToken);
            }

            EmployeeHandlerHelpers.AddAudit(
                _db,
                _currentUserService,
                tenantId,
                "EmployeeCreated",
                employee,
                new { employee.Code, role = request.Role, branchIds = request.BranchIds.Distinct().ToList() });

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var roles = new Dictionary<string, string> { [identityUserId] = request.Role };
            var list = await EmployeeHandlerHelpers.MapManyAsync(_db, _identityService, [employee], roles, cancellationToken);
            return list.Single();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            if (photoUrl is not null)
                await _files.DeleteAsync(photoUrl, cancellationToken);
            throw;
        }
    }
}
