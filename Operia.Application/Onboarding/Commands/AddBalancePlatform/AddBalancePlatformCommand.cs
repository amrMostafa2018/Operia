using MediatR;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Commands.AddBalancePlatform;

public sealed record AddBalancePlatformCommand(
    decimal Amount,
    string ScreenShotUrl) : IRequest<AddBalancePlatformResultDto>;
