namespace Operia.Application.Onboarding.DTOs;

public sealed record TenantUploadContextDto(
    string TenantId,
    bool IsNewTenant);
