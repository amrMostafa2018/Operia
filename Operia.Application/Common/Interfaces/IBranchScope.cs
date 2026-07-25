namespace Operia.Application.Common.Interfaces;

public interface IBranchScope
{
    Task<IReadOnlyCollection<string>> GetAllowedBranchIdsAsync(string userId, CancellationToken cancellationToken = default);
}
