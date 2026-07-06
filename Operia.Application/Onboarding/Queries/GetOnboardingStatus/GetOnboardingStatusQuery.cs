using MediatR;
using Operia.Application.Onboarding.DTOs;

namespace Operia.Application.Onboarding.Queries.GetOnboardingStatus;

public sealed record GetOnboardingStatusQuery : IRequest<OnboardingStatusDto>;
