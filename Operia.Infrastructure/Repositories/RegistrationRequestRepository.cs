using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Repositories;

public sealed class RegistrationRequestRepository : IRegistrationRequestRepository
{
    private readonly ApplicationDbContext _context;

    public RegistrationRequestRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<RegistrationRequest?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _context.RegistrationRequests
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RegistrationRequest>> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
        => await _context.RegistrationRequests
            .Where(x => x.Email == email)
            .ToListAsync(cancellationToken);

    public Task AddAsync(RegistrationRequest request, CancellationToken cancellationToken = default)
        => _context.RegistrationRequests.AddAsync(request, cancellationToken).AsTask();

    public void Remove(RegistrationRequest request)
        => _context.RegistrationRequests.Remove(request);

    public void RemoveRange(IEnumerable<RegistrationRequest> requests)
        => _context.RegistrationRequests.RemoveRange(requests);
}
