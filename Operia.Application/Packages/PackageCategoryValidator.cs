using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.Exceptions;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages;

internal static class PackageCategoryValidator
{
    public static async Task ValidateAsync(
        IApplicationDbContext db,
        string businessId,
        string serviceCategoryId,
        string? subServiceCategoryId,
        CancellationToken cancellationToken)
    {
        var categoryExists = await db.ServiceCategories.AnyAsync(
            category => category.Id == serviceCategoryId && category.BusinessId == businessId,
            cancellationToken);

        if (!categoryExists)
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "serviceCategoryId",
                    ApiErrorCodes.Packages.PackageCategoryRequired)
                {
                    ErrorCode = ApiErrorCodes.Packages.PackageCategoryRequired
                }
            ]);
        }

        if (string.IsNullOrWhiteSpace(subServiceCategoryId))
        {
            return;
        }

        var subCategoryExists = await db.SubServiceCategories.AnyAsync(
            category => category.Id == subServiceCategoryId
                        && category.BusinessId == businessId
                        && category.ServiceCategoryId == serviceCategoryId,
            cancellationToken);

        if (!subCategoryExists)
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "subServiceCategoryId",
                    ApiErrorCodes.Packages.SubCategoryParentNotFound)
                {
                    ErrorCode = ApiErrorCodes.Packages.SubCategoryParentNotFound
                }
            ]);
        }
    }
}
