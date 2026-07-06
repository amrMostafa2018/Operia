using Microsoft.EntityFrameworkCore;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Persistence;

namespace Operia.Infrastructure.Repositories;

public sealed class BusinessGalleryRepository : IBusinessGalleryRepository
{
    private readonly ApplicationDbContext _context;

    public BusinessGalleryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<BusinessGallery?> GetMainImageByBusinessIdAsync(
        string businessId,
        CancellationToken cancellationToken = default)
        => _context.BusinessGalleries
            .FirstOrDefaultAsync(g => g.BusinessId == businessId && g.IsMainImage, cancellationToken);

    public Task AddAsync(BusinessGallery gallery, CancellationToken cancellationToken = default)
        => _context.BusinessGalleries.AddAsync(gallery, cancellationToken).AsTask();
}
