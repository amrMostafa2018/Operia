using MediatR;
using Operia.Application.Common.Models;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Commands.AddBalancePlatform;

public sealed record AddBalancePlatformCommand(
    decimal Amount,
    FileUploadContent? Screenshot) : IRequest<AddBalancePlatformResultDto>;
