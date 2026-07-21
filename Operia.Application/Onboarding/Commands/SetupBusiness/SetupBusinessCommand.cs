using MediatR;
using Operia.Application.Common.Models;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Enums;

namespace Operia.Application.Onboarding.Commands.SetupBusiness;

public sealed record SetupBusinessCommand(
    string BusinessName,
    BusinessType BusinessType,
    string CountryCode,
    string City,
    string CurrencyCode,
    FileUploadContent? Logo = null) : IRequest<SetupBusinessResultDto>;
