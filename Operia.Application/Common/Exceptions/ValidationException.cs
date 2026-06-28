using FluentValidation.Results;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Common.Exceptions;

public sealed class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public IDictionary<string, string[]> ErrorCodes { get; }

    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
        ErrorCodes = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures, string language = "en")
        : this()
    {
        var normalized = failures
            .Select(f => new ValidationFailure(
                ValidationFailureFactory.NormalizeFieldName(f.PropertyName),
                string.IsNullOrWhiteSpace(f.ErrorCode)
                    ? f.ErrorMessage
                    : ApiErrorCatalog.GetMessage(f.ErrorCode, language))
            {
                ErrorCode = f.ErrorCode
            })
            .ToList();

        Errors = normalized
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());

        ErrorCodes = normalized
            .Where(f => !string.IsNullOrWhiteSpace(f.ErrorCode))
            .GroupBy(f => f.PropertyName, f => f.ErrorCode!)
            .ToDictionary(g => g.Key, g => g.Distinct().ToArray());
    }
}
