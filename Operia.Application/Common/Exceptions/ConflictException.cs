namespace Operia.Application.Common.Exceptions;

public sealed class ConflictException : Exception
{
    public ConflictException(string message, IReadOnlyList<BranchDependencyInfo>? dependencies = null)
        : base(message)
    {
        Dependencies = dependencies ?? [];
    }

    public IReadOnlyList<BranchDependencyInfo> Dependencies { get; }
}

public sealed record BranchDependencyInfo(string Type, int Count);
