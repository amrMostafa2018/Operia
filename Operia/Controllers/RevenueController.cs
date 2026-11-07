using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Revenue.Queries.ListInstallmentRevenue;
using Operia.Application.Revenue.Queries.ListActivityRevenue;
using Operia.Application.Revenue.Queries.GetActivityRevenueSummary;
using Operia.Application.Revenue.Queries.GetRevenueBranches;
using Operia.Application.Revenue.Queries.GetRevenueServices;
using Operia.Application.Revenue.Queries.GetActivityRevenueReport;
using Operia.Application.Revenue.Queries.ExportActivityRevenue;
using Operia.Application.Revenue.Commands.SubmitRevenuePayment;
using Operia.Application.Revenue.Commands.ReviewRevenuePayment;
using Operia.Application.Revenue.Queries.GetRevenuePaymentProof;
using Operia.Application.Revenue.Queries.GetRevenuePaymentHistory;
using Operia.Application.Common.Exceptions;
using Operia.SharedKernel.Errors;
using FluentValidation.Results;

namespace Operia.Controllers;

[ApiController]
[Route("api/revenue")]
public sealed class RevenueController(IMediator mediator) : ControllerBase
{
    [HttpPost("payments/review")]
    [Authorize(Policy = Policies.RevenueReview)]
    [Consumes("multipart/form-data")]
    public async Task<SubmittedRevenuePayment> SubmitPaymentReview(
        [FromForm] string saleId,
        [FromForm] string idempotencyKey,
        IFormFile? proof,
        CancellationToken cancellationToken)
    {
        byte[]? content = null;
        if (proof is not null)
        {
            if (proof.Length is <= 0 or > 5_242_880)
            {
                throw new ValidationException([
                    new ValidationFailure("proof", ApiErrorCodes.Revenue.ProofInvalid)
                    { ErrorCode = ApiErrorCodes.Revenue.ProofInvalid }
                ]);
            }
            await using var stream = new MemoryStream();
            await proof.CopyToAsync(stream, cancellationToken);
            content = stream.ToArray();
        }
        return await mediator.Send(new SubmitRevenuePaymentCommand(
            saleId, idempotencyKey, content, proof?.ContentType), cancellationToken);
    }

    [HttpPost("payments/{paymentId}/confirm")]
    [Authorize(Policy = Policies.RevenueReview)]
    public Task<ReviewedRevenuePayment> ConfirmPayment(
        string paymentId, CancellationToken cancellationToken) =>
        mediator.Send(new ReviewRevenuePaymentCommand(paymentId, true), cancellationToken);

    [HttpPost("payments/{paymentId}/cancel")]
    [Authorize(Policy = Policies.RevenueReview)]
    public Task<ReviewedRevenuePayment> CancelPayment(
        string paymentId, CancellationToken cancellationToken) =>
        mediator.Send(new ReviewRevenuePaymentCommand(paymentId, false), cancellationToken);

    [HttpGet("payments/{paymentId}/proof")]
    [Authorize(Policy = Policies.RevenueRead)]
    public async Task<FileContentResult> PaymentProof(
        string paymentId, CancellationToken cancellationToken)
    {
        var proof = await mediator.Send(new GetRevenuePaymentProofQuery(paymentId),
            cancellationToken);
        return File(proof.Content, proof.ContentType);
    }

    [HttpGet("payments/{paymentId}/history")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<IReadOnlyList<RevenuePaymentHistoryRow>> PaymentHistory(
        string paymentId, CancellationToken cancellationToken) =>
        mediator.Send(new GetRevenuePaymentHistoryQuery(paymentId), cancellationToken);

    [HttpGet("services")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<IReadOnlyList<RevenueServiceDto>> Services(CancellationToken cancellationToken) =>
        mediator.Send(new GetRevenueServicesQuery(), cancellationToken);

    [HttpGet("report")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<ActivityRevenueReport> Report(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? branchId = null,
        [FromQuery] string? serviceId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetActivityRevenueReportQuery(
            fromDate, toDate, branchId, serviceId, page, pageSize), cancellationToken);

    [HttpGet("report/export")]
    [Authorize(Policy = Policies.RevenueExport)]
    public async Task<FileContentResult> ExportReport(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? branchId = null,
        [FromQuery] string? serviceId = null,
        CancellationToken cancellationToken = default)
    {
        var workbook = await mediator.Send(new ExportActivityRevenueQuery(
            fromDate, toDate, branchId, serviceId), cancellationToken);
        return File(workbook,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "activity-revenue.xlsx");
    }

    [HttpGet("branches")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<IReadOnlyList<RevenueBranchDto>> Branches(CancellationToken cancellationToken) =>
        mediator.Send(new GetRevenueBranchesQuery(), cancellationToken);

    [HttpGet("installment-collections")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<InstallmentRevenuePage> InstallmentCollections(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        mediator.Send(new ListInstallmentRevenueQuery(page, pageSize), cancellationToken);

    [HttpGet("collections")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<ActivityRevenuePage> Collections(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? branchId = null) =>
        mediator.Send(new ListActivityRevenueQuery(page, pageSize, branchId), cancellationToken);

    [HttpGet("summary")]
    [Authorize(Policy = Policies.RevenueRead)]
    public Task<ActivityRevenueSummaryDto> Summary(
        CancellationToken cancellationToken,
        [FromQuery] string? branchId = null) =>
        mediator.Send(new GetActivityRevenueSummaryQuery(branchId), cancellationToken);
}
