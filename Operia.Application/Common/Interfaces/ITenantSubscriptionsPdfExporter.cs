using Operia.Application.Finance.DTOs;
using Operia.Application.Finance.Resources;

namespace Operia.Application.Common.Interfaces;

public interface ITenantSubscriptionsPdfExporter
{
    byte[] Export(
        IReadOnlyList<TenantSubscriptionDto> subscriptions,
        FinanceResources financeResources);
}
