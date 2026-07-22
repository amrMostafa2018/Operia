using MediatR;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Settings;

public sealed record GetIdentitySettingsQuery : IRequest<IdentitySettingsDto>;
public sealed record UpdateIdentitySettingsCommand(UpdateIdentitySettingsRequest Request) : IRequest<IdentitySettingsDto>;
public sealed record GetPaymentMethodsQuery : IRequest<PaymentMethodsDto>;
public sealed record UpdatePaymentMethodsCommand(PaymentMethodsDto Request) : IRequest<PaymentMethodsDto>;
public sealed record GetWorkingDaysQuery : IRequest<WorkingDaysSettingsDto>;
public sealed record UpdateWorkingDaysCommand(WorkingDaysSettingsDto Request) : IRequest<WorkingDaysSettingsDto>;
public sealed record GetSecuritySettingsQuery : IRequest<SecuritySettingsDto>;
public sealed record UpdateSecuritySettingsCommand(UpdateSecurityRequest Request) : IRequest;
public sealed record SendPasswordOtpCommand : IRequest;
public sealed record ChangePasswordCommand(ChangePasswordRequest Request) : IRequest;
public sealed record BanSettingsUserCommand(string UserId) : IRequest;
public sealed record DeleteSettingsUserCommand(string UserId) : IRequest;
public sealed record DeactivateAccountCommand : IRequest;

public sealed class SettingsHandler : IRequestHandler<GetIdentitySettingsQuery, IdentitySettingsDto>, IRequestHandler<UpdateIdentitySettingsCommand, IdentitySettingsDto>, IRequestHandler<GetPaymentMethodsQuery, PaymentMethodsDto>, IRequestHandler<UpdatePaymentMethodsCommand, PaymentMethodsDto>, IRequestHandler<GetWorkingDaysQuery, WorkingDaysSettingsDto>, IRequestHandler<UpdateWorkingDaysCommand, WorkingDaysSettingsDto>, IRequestHandler<GetSecuritySettingsQuery, SecuritySettingsDto>, IRequestHandler<UpdateSecuritySettingsCommand>, IRequestHandler<SendPasswordOtpCommand>, IRequestHandler<ChangePasswordCommand>, IRequestHandler<BanSettingsUserCommand>, IRequestHandler<DeleteSettingsUserCommand>, IRequestHandler<DeactivateAccountCommand>
{
    private readonly ISettingsService _settings;
    private readonly ICurrentUserService _current;
    public SettingsHandler(ISettingsService settings, ICurrentUserService current) => (_settings, _current) = (settings, current);
    private string TenantId => _current.TenantId ?? throw new UnauthorizedAccessException();
    private string UserId => _current.UserId ?? throw new UnauthorizedAccessException();
    public Task<IdentitySettingsDto> Handle(GetIdentitySettingsQuery request, CancellationToken ct) => _settings.GetIdentityAsync(TenantId, ct);
    public Task<IdentitySettingsDto> Handle(UpdateIdentitySettingsCommand request, CancellationToken ct) => _settings.UpdateIdentityAsync(TenantId, request.Request, ct);
    public Task<PaymentMethodsDto> Handle(GetPaymentMethodsQuery request, CancellationToken ct) => _settings.GetPaymentMethodsAsync(TenantId, ct);
    public Task<PaymentMethodsDto> Handle(UpdatePaymentMethodsCommand request, CancellationToken ct) => _settings.UpdatePaymentMethodsAsync(TenantId, request.Request, ct);
    public Task<WorkingDaysSettingsDto> Handle(GetWorkingDaysQuery request, CancellationToken ct) => _settings.GetWorkingDaysAsync(TenantId, ct);
    public Task<WorkingDaysSettingsDto> Handle(UpdateWorkingDaysCommand request, CancellationToken ct) => _settings.UpdateWorkingDaysAsync(TenantId, request.Request, ct);
    public Task<SecuritySettingsDto> Handle(GetSecuritySettingsQuery request, CancellationToken ct) => _settings.GetSecurityAsync(UserId, ct);
    public Task Handle(UpdateSecuritySettingsCommand request, CancellationToken ct) => _settings.UpdateSecurityAsync(UserId, request.Request, ct);
    public Task Handle(SendPasswordOtpCommand request, CancellationToken ct) => _settings.SendPasswordOtpAsync(UserId, ct);
    public Task Handle(ChangePasswordCommand request, CancellationToken ct) => _settings.ChangePasswordAsync(UserId, request.Request, ct);
    public Task Handle(BanSettingsUserCommand request, CancellationToken ct) => _settings.BanUserAsync(UserId, request.UserId, ct);
    public Task Handle(DeleteSettingsUserCommand request, CancellationToken ct) => _settings.DeleteUserAsync(UserId, request.UserId, ct);
    public Task Handle(DeactivateAccountCommand request, CancellationToken ct) => _settings.DeactivateAccountAsync(UserId, ct);
}
