using Operia.Domain.Entities;

namespace Operia.Domain.Interfaces;

public interface IRegistrationRequestRepository
{
    Task<RegistrationRequest?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegistrationRequest>> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task AddAsync(RegistrationRequest request, CancellationToken cancellationToken = default);

    void Remove(RegistrationRequest request);

    void RemoveRange(IEnumerable<RegistrationRequest> requests);
}
