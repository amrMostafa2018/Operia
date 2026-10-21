using System.Globalization;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.Exceptions;
using Operia.SharedKernel.Errors;
using Operia.Application.Common.PhoneNumbers;
using Operia.Domain.Entities;

namespace Operia.Application.Branches;

//TODO: to be replaced with automapper
internal static class BranchMapper
{
    public static string RequireTenant(ICurrentUserService currentUser) =>
        currentUser.TenantId
        ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);

    public static Branch CreateEntity(
        string tenantId,
        string businessId,
        string name,
        string address,
        string phoneNumber,
        decimal latitude,
        decimal longitude) => new()
        {
            TenantId = tenantId,
            BusinessId = businessId,
            Name = name,
            Address = address.Trim(),
            PhoneNumber = PhoneNumberHelper.ToE164(phoneNumber),
            Latitude = latitude,
            Longitude = longitude,
            GoogleMapsUrl = MapsUrl(latitude, longitude)
        };

    public static string MapsUrl(decimal latitude, decimal longitude) =>
        $"https://www.google.com/maps?q={latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}";

    public static BranchDto ToDto(Branch branch) => new(
        branch.Id,
        branch.Name,
        branch.Address,
        branch.PhoneNumber,
        branch.Latitude,
        branch.Longitude,
        branch.GoogleMapsUrl);

}
