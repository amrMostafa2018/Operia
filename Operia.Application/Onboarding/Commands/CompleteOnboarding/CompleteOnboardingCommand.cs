using MediatR;
using Operia.Application.Onboarding.DTOs;
using Operia.Domain.Enums;

namespace Operia.Application.Onboarding.Commands.CompleteOnboarding;

public sealed record CompleteOnboardingCommand(
    string PlanId,
    BillingType BillingType,
    string ScreenShotUrl) : IRequest<OnboardingResultDto>;
