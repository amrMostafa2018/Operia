using FluentValidation;
using FluentValidation.Results;
using Operia.Application.Packages.DTOs;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages;

internal static class InstallmentPlanRules
{
    public static int PrepaymentReleaseLimit(int sessions) =>
        Math.Min(Math.Max(0, sessions - 1), (int)Math.Ceiling(sessions * 0.7m - 0.5m));

    public static void Apply<T>(
        AbstractValidator<T> validator,
        Func<T, bool> enabled,
        Func<T, string> offerType,
        Func<T, decimal?> price,
        Func<T, decimal?> firstPayment,
        Func<T, int?> installmentCount,
        Func<T, int?> sessionCount,
        Func<T, IReadOnlyList<PackageInstallmentInput>?> rows)
    {
        validator.RuleFor(command => command).Custom((command, context) =>
        {
            var planRows = rows(command);
            if (!enabled(command))
            {
                if (price(command).HasValue || firstPayment(command).HasValue ||
                    installmentCount(command).HasValue ||
                    planRows is { Count: > 0 })
                {
                    Add(context, "installments", ApiErrorCodes.Packages.PackageInstallmentPayloadNotAllowed);
                }
                return;
            }

            if (!string.Equals(offerType(command), "package", StringComparison.OrdinalIgnoreCase))
            {
                Add(context, "installments", ApiErrorCodes.Packages.PackageInstallmentOnlyPackage);
                return;
            }

            var installmentPrice = price(command);
            var upfront = firstPayment(command);
            if (!upfront.HasValue)
            {
                Add(context, "firstPayment", ApiErrorCodes.Packages.PackageFirstPaymentRequired);
            }
            else if (upfront < 0 || decimal.Round(upfront.Value, 2) != upfront ||
                (installmentPrice.HasValue && upfront > installmentPrice))
            {
                Add(context, "firstPayment", ApiErrorCodes.Packages.PackageFirstPaymentRange);
            }

            var count = installmentCount(command);
            if (count is null or < 1)
            {
                Add(context, "installmentCount", ApiErrorCodes.Packages.PackageInstallmentCountRange);
            }
            if (planRows is null || planRows.Count != count)
            {
                Add(context, "installments", ApiErrorCodes.Packages.PackageInstallmentRowsCount);
                return;
            }

            if (planRows.Any(row => row is null || row.Amount < 0 ||
                decimal.Round(row.Amount, 2) != row.Amount))
            {
                Add(context, "installments", ApiErrorCodes.Packages.PackageInstallmentAmountInvalid);
                return;
            }
            if (installmentPrice.HasValue && upfront.HasValue &&
                planRows.Sum(row => row.Amount) != installmentPrice - upfront)
            {
                Add(context, "installments", ApiErrorCodes.Packages.PackageInstallmentTotalMismatch);
            }

            var sessions = sessionCount(command).GetValueOrDefault();
            if (sessions <= 0)
            {
                Add(context, "installments", ApiErrorCodes.Packages.PackageInstallmentSessionsInvalid);
                return;
            }
            var cumulativeSessions = 0;
            var cumulativePayment = upfront.GetValueOrDefault();
            foreach (var row in planRows)
            {
                if (row.SessionsUnlocked < 0 || row.SessionsUnlocked > sessions - cumulativeSessions)
                {
                    Add(context, "installments", ApiErrorCodes.Packages.PackageInstallmentSessionsInvalid);
                    return;
                }
                cumulativeSessions += row.SessionsUnlocked;
                cumulativePayment += row.Amount;
                if (installmentPrice.HasValue && cumulativePayment < installmentPrice &&
                    cumulativeSessions > PrepaymentReleaseLimit(sessions))
                {
                    Add(context, "installments", ApiErrorCodes.Packages.PackageInstallmentReleaseCap);
                    return;
                }
            }
            if (cumulativeSessions != sessions)
            {
                Add(context, "installments", ApiErrorCodes.Packages.PackageInstallmentSessionsInvalid);
            }
        });
    }

    public static void ReplaceRows(Package package, string tenantId, IReadOnlyList<PackageInstallmentInput>? rows)
    {
        var existing = package.Installments.ToDictionary(row => row.Number);
        foreach (var obsolete in package.Installments.Where(row => row.Number > (rows?.Count ?? 0)).ToArray())
        {
            package.Installments.Remove(obsolete);
        }
        var cumulative = 0;
        for (var index = 0; index < (rows?.Count ?? 0); index++)
        {
            var row = rows![index];
            cumulative += row.SessionsUnlocked;
            if (existing.TryGetValue(index + 1, out var current))
            {
                current.Amount = row.Amount;
                current.SessionsUnlocked = row.SessionsUnlocked;
                current.CumulativeAvailableSessions = cumulative;
                continue;
            }
            package.Installments.Add(new PackageInstallment
            {
                TenantId = tenantId,
                PackageId = package.Id,
                Number = index + 1,
                Amount = row.Amount,
                SessionsUnlocked = row.SessionsUnlocked,
                CumulativeAvailableSessions = cumulative
            });
        }
    }

    private static void Add<T>(ValidationContext<T> context, string field, string code) =>
        context.AddFailure(new ValidationFailure(field, code) { ErrorCode = code });
}
