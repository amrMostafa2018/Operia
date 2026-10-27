using MediatR;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Bookings.Queries.FindBookingCustomer;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Commands.RegisterBookingCustomer;

public sealed class RegisterBookingCustomerHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditWriter auditWriter) : IRequestHandler<RegisterBookingCustomerCommand, RegisteredBookingCustomerResult>
{
    public async Task<RegisteredBookingCustomerResult> Handle(
        RegisterBookingCustomerCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var normalized = FindBookingCustomerHandler.NormalizeMobile(request.MobileNumber);
        var alternate = normalized.StartsWith("20", StringComparison.Ordinal) && normalized.Length == 12
            ? "0" + normalized[2..]
            : normalized;
        if (!PhoneNumberHelper.IsValidMobile(request.MobileNumber))
        {
            throw new ValidationException([new ValidationFailure("mobileNumber", ApiErrorCodes.Auth.PhoneInvalid)
            {
                ErrorCode = ApiErrorCodes.Auth.PhoneInvalid
            }]);
        }

        if (await db.Customers.AsNoTracking().AnyAsync(x =>
                x.TenantId == tenantId && !x.IsDirect &&
                (x.NormalizedMobileNumber == normalized || x.NormalizedMobileNumber == alternate),
                cancellationToken))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.CustomerMobileExists, "mobileNumber");
        }

        var customer = new Customer
        {
            TenantId = tenantId,
            FullName = request.FullName.Trim(),
            MobileNumber = request.MobileNumber.Trim(),
            NormalizedMobileNumber = normalized
        };
        db.Customers.Add(customer);
        auditWriter.Write(tenantId, AuditActions.CustomerCreated, nameof(Customer), customer.Id,
            customer.FullName, new { customer.FullName, customer.MobileNumber });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await db.Customers.AsNoTracking().AnyAsync(x =>
                    x.TenantId == tenantId && !x.IsDirect &&
                    (x.NormalizedMobileNumber == normalized || x.NormalizedMobileNumber == alternate),
                    cancellationToken))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Bookings.CustomerMobileExists, "mobileNumber");
            }

            throw;
        }

        return new RegisteredBookingCustomerResult(customer.Id, customer.FullName, customer.MobileNumber);
    }
}
