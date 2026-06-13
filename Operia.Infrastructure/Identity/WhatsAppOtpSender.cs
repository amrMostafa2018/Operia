using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Operia.Application.Common.Interfaces;
using Operia.Infrastructure.Options;

namespace Operia.Infrastructure.Identity;

public sealed class WhatsAppOtpSender : IOtpSender
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppSettings _settings;
    private readonly ILogger<WhatsAppOtpSender> _logger;

    public WhatsAppOtpSender(
        HttpClient httpClient,
        IOptions<WhatsAppSettings> settings,
        ILogger<WhatsAppOtpSender> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiUrl))
        {
            _logger.LogInformation(
                "WhatsApp OTP stub — Phone: {PhoneNumber}, Code: {Code}",
                phoneNumber,
                code);
            return;
        }

        var payload = new
        {
            to = phoneNumber,
            message = $"Your Operia verification code is: {code}"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ApiUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.Token);
        request.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
