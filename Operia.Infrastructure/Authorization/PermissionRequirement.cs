using Microsoft.AspNetCore.Authorization;

namespace Operia.Infrastructure.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
