using Operia.SharedKernel.Errors;

namespace Operia.Application.Common.Exceptions;

public sealed class ConflictException : Exception
{
    public ConflictException(string message, IReadOnlyList<BranchDependencyInfo>? dependencies = null)
        : base(message)
    {
        Dependencies = dependencies ?? [];
    }

    public IReadOnlyList<BranchDependencyInfo> Dependencies { get; }

    public string? ErrorCode { get; }

    public string Field { get; } = "detail";

    public static ConflictException FromCode(
        string errorCode,
        string field = "detail",
        IReadOnlyList<BranchDependencyInfo>? dependencies = null) =>
        new(errorCode, field, dependencies, isErrorCode: true);

    private ConflictException(
        string errorCode,
        string field,
        IReadOnlyList<BranchDependencyInfo>? dependencies,
        bool isErrorCode)
        : base(errorCode)
    {
        Field = field;
        ErrorCode = errorCode;
        Dependencies = dependencies ?? [];
    }
}

public sealed record BranchDependencyInfo(string Type, int Count);
