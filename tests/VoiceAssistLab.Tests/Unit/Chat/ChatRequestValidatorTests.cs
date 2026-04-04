using FluentAssertions;
using VoiceAssistLab.Core.Chat;

namespace VoiceAssistLab.Tests.Unit.Chat;

public sealed class ChatRequestValidatorTests
{
    private readonly ChatRequestValidator _sut = new();

    [Fact]
    public async Task Validate_ValidMessage_Passes()
    {
        var request = new ChatRequest("What is the return policy?");

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyMessage_Fails()
    {
        var request = new ChatRequest("");

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Message");
    }

    [Fact]
    public async Task Validate_MessageOver500Chars_Fails()
    {
        var request = new ChatRequest(new string('a', 501));

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Message");
    }

    [Fact]
    public async Task Validate_MessageExactly500Chars_Passes()
    {
        var request = new ChatRequest(new string('a', 500));

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }
}
