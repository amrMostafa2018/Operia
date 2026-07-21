using FluentValidation;
using Operia.Application.Common.PhoneNumbers;

namespace Operia.Application.Branches.Commands.CreateBranch;

public sealed class CreateBranchValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Address).NotEmpty().MaximumLength(500);
        RuleFor(command => command.PhoneNumber)
            .Must(PhoneNumberHelper.IsValid)
            .WithMessage("A valid branch phone number is required.");
        RuleFor(command => command.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(command => command.Longitude).InclusiveBetween(-180m, 180m);
    }
}
