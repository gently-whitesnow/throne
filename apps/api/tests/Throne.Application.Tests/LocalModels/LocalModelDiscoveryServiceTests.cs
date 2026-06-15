using FluentAssertions;
using NSubstitute;
using Throne.Application.LocalModels;
using Throne.Application.Ports;

namespace Throne.Application.Tests.LocalModels;

public class LocalModelDiscoveryServiceTests
{
    private static readonly string[] TwoModels = ["model-a", "model-b"];

    private readonly ILocalModelCatalogPort _catalog = Substitute.For<ILocalModelCatalogPort>();

    private LocalModelDiscoveryService Service(string? baseUrl) =>
        new(new LocalModelSettings { BaseUrl = baseUrl }, _catalog);

    [Theory(DisplayName = "Пустой/whitespace BaseUrl → not_configured без обращения к endpoint")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_base_url_not_configured(string? baseUrl)
    {
        var result = await Service(baseUrl).DiscoverAsync(CancellationToken.None);

        result.Status.Should().Be(LocalModelDiscoveryStatus.NotConfigured);
        result.BaseUrl.Should().BeNull();
        result.Models.Should().BeEmpty();
        await _catalog.DidNotReceive().ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Endpoint вернул модели → ready с нормализованным списком и trimmed BaseUrl")]
    public async Task Models_returned_ready()
    {
        _catalog.ListModelIdsAsync("http://localhost:1234", Arg.Any<CancellationToken>())
            .Returns(TwoModels);

        var result = await Service("  http://localhost:1234  ").DiscoverAsync(CancellationToken.None);

        result.Status.Should().Be(LocalModelDiscoveryStatus.Ready);
        result.BaseUrl.Should().Be("http://localhost:1234");
        result.Models.Should().Equal("model-a", "model-b");
    }

    [Fact(DisplayName = "Endpoint доступен, но моделей нет → empty")]
    public async Task No_models_empty()
    {
        _catalog.ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        var result = await Service("http://localhost:1234").DiscoverAsync(CancellationToken.None);

        result.Status.Should().Be(LocalModelDiscoveryStatus.Empty);
        result.Models.Should().BeEmpty();
    }

    [Fact(DisplayName = "Сбой probe → unreachable с текстом ошибки, без исключения наружу")]
    public async Task Probe_failure_unreachable()
    {
        _catalog.ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(_ => throw new HttpRequestException("connection refused"));

        var result = await Service("http://localhost:1234").DiscoverAsync(CancellationToken.None);

        result.Status.Should().Be(LocalModelDiscoveryStatus.Unreachable);
        result.Error.Should().Contain("connection refused");
    }

    [Fact(DisplayName = "Отмена пробрасывается наружу, а не маскируется под unreachable")]
    public async Task Cancellation_propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _catalog.ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(_ => throw new OperationCanceledException());

        var act = () => Service("http://localhost:1234").DiscoverAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
