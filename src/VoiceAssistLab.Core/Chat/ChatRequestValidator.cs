using FluentValidation;

namespace VoiceAssistLab.Core.Chat;

public sealed class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message cannot be empty.")
            .MaximumLength(500).WithMessage("Message cannot exceed 500 characters.");
    }
}
