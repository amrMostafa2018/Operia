using Operia.Domain.Entities;

namespace Operia.Domain.Interfaces;

public interface IBusinessGalleryRepository
{
    Task<BusinessGallery?> GetMainImageByBusinessIdAsync(
        string businessId,
        CancellationToken cancellationToken = default);

    Task AddAsync(BusinessGallery gallery, CancellationToken cancellationToken = default);
}
