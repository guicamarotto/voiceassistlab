using System.Text.Json;
using FluentAssertions;
using VoiceAssistLab.Infra.MockData;

namespace VoiceAssistLab.Tests.Unit.Tools;

public sealed class GetReturnPolicyToolTests
{
    private readonly MockDataRepository _repo = new();
    private readonly GetReturnPolicyTool _sut;

    public GetReturnPolicyToolTests() => _sut = new GetReturnPolicyTool(_repo);

    [Fact]
    public async Task ExecuteAsync_AlwaysReturnsReturnPolicyJson()
    {
        var args = JsonSerializer.SerializeToElement(new { });

        var result = await _sut.ExecuteAsync(args);

        result.Should().Contain("30 dias");
        result.Should().Contain("Loja Demo");
    }

    [Fact]
    public void Name_ReturnsExpectedToolName()
    {
        _sut.Name.Should().Be("get_return_policy");
    }
}
