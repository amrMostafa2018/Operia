using Operia.Domain.Entities;

namespace Operia.Domain.Interfaces;

public interface IBusinessRepository
{
    Task AddAsync(Business business, CancellationToken cancellationToken = default);
}
