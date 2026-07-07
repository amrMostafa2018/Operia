using MediatR;

namespace Operia.Application.Admin.Commands.ApproveAddBalancePlatform;

public sealed record ApproveAddBalancePlatformCommand(string RevenueId) : IRequest;
