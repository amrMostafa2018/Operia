using FluentValidation.Results;

namespace Operia.Application.Common.Exceptions;

public sealed class UnprocessableEntityException : ValidationException
{
    public UnprocessableEntityException(IEnumerable<ValidationFailure> failures)
        : base(failures)
    {
    }
}
