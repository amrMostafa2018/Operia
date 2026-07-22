using Operia.Application.Settings;

namespace Operia.Application.Common.Interfaces;

public interface ISettingsService
{
    Task<IdentitySettingsDto> GetIdentityAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<IdentitySettingsDto> UpdateIdentityAsync(string tenantId, UpdateIdentitySettingsRequest request, CancellationToken cancellationToken = default);
    Task<PaymentMethodsDto> GetPaymentMethodsAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<PaymentMethodsDto> UpdatePaymentMethodsAsync(string tenantId, PaymentMethodsDto request, CancellationToken cancellationToken = default);
    Task<WorkingDaysSettingsDto> GetWorkingDaysAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<WorkingDaysSettingsDto> UpdateWorkingDaysAsync(string tenantId, WorkingDaysSettingsDto request, CancellationToken cancellationToken = default);
    Task<SecuritySettingsDto> GetSecurityAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdateSecurityAsync(string userId, UpdateSecurityRequest request, CancellationToken cancellationToken = default);
    Task SendPasswordOtpAsync(string userId, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task BanUserAsync(string userId, string targetUserId, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(string userId, string targetUserId, CancellationToken cancellationToken = default);
    Task DeactivateAccountAsync(string userId, CancellationToken cancellationToken = default);
}
