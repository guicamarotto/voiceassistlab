using System.Text.Json;
using FluentAssertions;
using VoiceAssistLab.Infra.MockData;

namespace VoiceAssistLab.Tests.Unit.Tools;

public sealed class GetOrderStatusToolTests
{
    private readonly MockDataRepository _repo = new();
    private readonly GetOrderStatusTool _sut;

    public GetOrderStatusToolTests() => _sut = new GetOrderStatusTool(_repo);

    [Fact]
    public async Task ExecuteAsync_ExistingOrder_ReturnsJsonWithOrderData()
    {
        var args = JsonSerializer.SerializeToElement(new { order_id = "ORD-001" });

        var result = await _sut.ExecuteAsync(args);

        result.Should().Contain("ORD-001");
        result.Should().Contain("delivered");
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentOrder_ReturnsNotFoundMessage()
    {
        var args = JsonSerializer.SerializeToElement(new { order_id = "ORD-999" });

        var result = await _sut.ExecuteAsync(args);

        result.Should().Contain("não encontrado");
    }

    [Fact]
    public async Task ExecuteAsync_MissingOrderIdParam_ReturnsErrorMessage()
    {
        var args = JsonSerializer.SerializeToElement(new { });

        var result = await _sut.ExecuteAsync(args);

        result.Should().Contain("obrigatório");
    }

    [Fact]
    public void Name_ReturnsExpectedToolName()
    {
        _sut.Name.Should().Be("get_order_status");
    }
}
