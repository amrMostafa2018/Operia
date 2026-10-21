using FluentValidation;

namespace Operia.Application.Branches.Commands.DeleteBranch;

public sealed class DeleteBranchValidator : AbstractValidator<DeleteBranchCommand>
{
    public DeleteBranchValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .MaximumLength(128);
    }
}
