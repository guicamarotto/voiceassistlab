using System.Text.Json;
using FluentAssertions;
using VoiceAssistLab.Infra.MockData;

namespace VoiceAssistLab.Tests.Unit.Tools;

public sealed class GetProductInfoToolTests
{
    private readonly MockDataRepository _repo = new();
    private readonly GetProductInfoTool _sut;

    public GetProductInfoToolTests() => _sut = new GetProductInfoTool(_repo);

    [Fact]
    public async Task ExecuteAsync_ExistingProduct_ReturnsJsonWithProductData()
    {
        var args = JsonSerializer.SerializeToElement(new { product_id = "PROD-001" });

        var result = await _sut.ExecuteAsync(args);

        result.Should().Contain("PROD-001");
        result.Should().Contain("Bluetooth");
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentProduct_ReturnsNotFoundMessage()
    {
        var args = JsonSerializer.SerializeToElement(new { product_id = "PROD-999" });

        var result = await _sut.ExecuteAsync(args);

        result.Should().Contain("não encontrado");
    }

    [Fact]
    public void Name_ReturnsExpectedToolName()
    {
        _sut.Name.Should().Be("get_product_info");
    }
}
