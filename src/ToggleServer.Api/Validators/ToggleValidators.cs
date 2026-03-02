using FluentValidation;
using ToggleServer.Core.Models;

namespace ToggleServer.Api.Validators;

public class CreateToggleRequestValidator : AbstractValidator<FeatureToggle>
{
    public CreateToggleRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleForEach(x => x.Rules).SetValidator(new ToggleRuleValidator());
    }
}

public class UpdateToggleRequestValidator : AbstractValidator<FeatureToggle>
{
    public UpdateToggleRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        // Version is crucial for update to prevent lost updates
        RuleFor(x => x.Version).GreaterThan(0).WithMessage("Version must be provided and greater than 0 for an update.");
        RuleForEach(x => x.Rules).SetValidator(new ToggleRuleValidator());
    }
}

public class ToggleRuleValidator : AbstractValidator<ToggleRule>
{
    public ToggleRuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleForEach(x => x.Conditions).SetValidator(new ToggleConditionValidator());
    }
}

public class ToggleConditionValidator : AbstractValidator<ToggleCondition>
{
    public ToggleConditionValidator()
    {
        RuleFor(x => x.Attribute).NotEmpty();
        // Operators like EQUALS, IN require Values
        RuleFor(x => x.Values).NotEmpty().When(x => x.Operator != ConditionOperator.PERCENTAGE_ROLLOUT);
    }
}
