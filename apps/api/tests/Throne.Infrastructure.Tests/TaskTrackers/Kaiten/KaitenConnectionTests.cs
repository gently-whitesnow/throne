using FluentAssertions;
using Throne.Infrastructure.TaskTrackers.Kaiten;

namespace Throne.Infrastructure.Tests.TaskTrackers.Kaiten;

public class KaitenConnectionTests
{
    [Theory(DisplayName = "ApiBaseUrl нормализует хвостовой слэш и добавляет /api/v1")]
    [InlineData("https://acme.kaiten.ru", "https://acme.kaiten.ru/api/v1")]
    [InlineData("https://acme.kaiten.ru/", "https://acme.kaiten.ru/api/v1")]
    public void Builds_api_base_url(string baseUrl, string expected) =>
        new KaitenConnection(baseUrl, "token").ApiBaseUrl.Should().Be(expected);
}
