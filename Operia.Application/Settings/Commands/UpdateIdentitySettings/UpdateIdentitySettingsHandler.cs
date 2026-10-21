using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Settings.Common;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Settings.Commands.UpdateIdentitySettings;

public sealed class UpdateIdentitySettingsHandler : IRequestHandler<UpdateIdentitySettingsCommand, IdentitySettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _files;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditWriter _auditWriter;

    public UpdateIdentitySettingsHandler(
        IApplicationDbContext db,
        IFileStorageService files,
        ICurrentUserService currentUserService,
        IAuditWriter auditWriter)
    {
        _db = db;
        _files = files;
        _currentUserService = currentUserService;
        _auditWriter = auditWriter;
    }

    public async Task<IdentitySettingsDto> Handle(UpdateIdentitySettingsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = SettingsHandlerHelpers.RequireTenant(_currentUserService);
        var business = await SettingsHandlerHelpers.GetBusinessAsync(_db, tenantId, cancellationToken);
        var model = request.Request;

        business.ActivityName = model.ActivityName.Trim();
        business.MobileNumber = model.ContactPhone?.Trim();
        business.WhatsappNumber = model.WhatsappPhone?.Trim();
        business.Email = model.Email?.Trim();
        business.MainBranchAddress = model.MainAddress?.Trim();
        business.Description = model.About?.Trim();

        var photos = await _db.BusinessGalleries
            .Where(x => x.BusinessId == business.Id)
            .OrderBy(x => x.UploadedAt)
            .ToListAsync(cancellationToken);

        var kept = model.ExistingPhotoUrls.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        if (kept.Count + model.NewPhotos.Count > 6)
        {
            throw new ValidationException(
            [
                new ValidationFailure("newPhotos", "")
                {
                    ErrorCode = ApiErrorCodes.Settings.GalleryPhotoLimitExceeded
                }
            ]);
        }

        foreach (var item in photos.Where(x => !kept.Contains(x.ImageUrl)).ToList())
        {
            await _files.DeleteAsync(item.ImageUrl, cancellationToken);
            _db.BusinessGalleries.Remove(item);
            photos.Remove(item);
        }

        foreach (var upload in model.NewPhotos)
        {
            if (upload.IsPrimary)
            {
                foreach (var previous in photos.Where(x => x.IsMainImage))
                    previous.IsMainImage = false;
            }

            var url = await _files.SaveAsync(upload.Content, upload.FileName, upload.ContentType, business.TenantId, FileUploadCategory.BusinessGallery, cancellationToken);
            var gallery = new BusinessGallery
            {
                BusinessId = business.Id,
                ImageUrl = url,
                IsMainImage = upload.IsPrimary,
                UploadedAt = DateTime.UtcNow
            };

            await _db.BusinessGalleries.AddAsync(gallery, cancellationToken);
            photos.Add(gallery);
        }

        if (photos.Count > 0 && photos.All(x => !x.IsMainImage))
            photos[0].IsMainImage = true;

        _auditWriter.Write(
            tenantId,
            AuditActions.BusinessIdentityUpdated,
            nameof(Business),
            business.Id,
            business.ActivityName,
            new { photoCount = photos.Count });
        await _db.SaveChangesAsync(cancellationToken);
        return SettingsHandlerHelpers.ToIdentity(business, photos.OrderByDescending(x => x.IsMainImage).ThenBy(x => x.UploadedAt));
    }
}
