using System.Net.Mail;
using FluentValidation;

namespace CrmIntegrationService.Leads;

public sealed class LeadRequestValidator : AbstractValidator<LeadRequest>
{
    public const int MaxEmailLength = 254;
    public const int MaxNameLength = 100;

    public LeadRequestValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .Must(email => !string.IsNullOrWhiteSpace(email)).WithMessage("Email is required.")
            .Must(email => email!.Trim().Length <= MaxEmailLength).WithMessage($"Email must be {MaxEmailLength} characters or fewer.")
            .Must(BeAPlainAddress).WithMessage("Email is not a valid address.");

        RuleFor(x => x.Name)
            .MaximumLength(MaxNameLength);
    }

    // Accept only a bare address ("ada@example.com"), not display-name forms MailAddress also parses.
    private static bool BeAPlainAddress(string? email)
    {
        var trimmed = email!.Trim();
        return MailAddress.TryCreate(trimmed, out var parsed) && parsed.Address == trimmed;
    }
}
