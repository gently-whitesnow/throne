using FluentAssertions;
using Throne.Infrastructure.LocalModels;

namespace Throne.Infrastructure.Tests.LocalModels;

public class OpenAiModelsNormalizerTests
{
    [Fact(DisplayName = "Стандартный /v1/models ответ → id в порядке выдачи")]
    public void Standard_payload_extracts_ids_in_order()
    {
        const string payload = """
        { "object": "list", "data": [
            { "id": "llama-3", "object": "model" },
            { "id": "qwen-2.5", "object": "model" }
        ] }
        """;

        OpenAiModelsNormalizer.Normalize(payload).Should().Equal("llama-3", "qwen-2.5");
    }

    [Fact(DisplayName = "Дубликаты схлопываются, пустые/whitespace id отбрасываются, остальные триммятся")]
    public void Dedupes_and_drops_blank_ids()
    {
        const string payload = """
        { "data": [
            { "id": "  model-a  " },
            { "id": "model-a" },
            { "id": "" },
            { "id": "   " },
            { "id": "model-b" }
        ] }
        """;

        OpenAiModelsNormalizer.Normalize(payload).Should().Equal("model-a", "model-b");
    }

    [Theory(DisplayName = "Пустой/отсутствующий data → пустой список")]
    [InlineData("{ \"data\": [] }")]
    [InlineData("{ \"object\": \"list\" }")]
    [InlineData("{}")]
    public void Empty_data_yields_empty(string payload)
    {
        OpenAiModelsNormalizer.Normalize(payload).Should().BeEmpty();
    }
}
