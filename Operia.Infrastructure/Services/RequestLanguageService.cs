using Microsoft.AspNetCore.Http;
using Operia.Application.Common.Interfaces;

namespace Operia.Infrastructure.Services;

public sealed class RequestLanguageService : IRequestLanguageService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestLanguageService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetLanguage() =>
        _httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage.FirstOrDefault() ?? "en";
}
