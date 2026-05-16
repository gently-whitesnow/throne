using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Throne.Api.Mcp;
using Throne.Application.Auth;
using Throne.Application.Ports;

namespace Throne.Api.Tests.Mcp;

// Regression suite for the create_intent(tags=[...]) crash described in the work bundle:
// the SDK-generated JSON Schema omits `type` for nullable IReadOnlyList<string>?, and the
// downstream AIFunction binder rejected JsonElement(Array). The fix lives in
// AuditingMcpServerTool.BuildAIFunctionArguments — these tests pin its contract through
// the real AddThroneTool<T>() pipeline so a future SDK regression cannot slip past.
public class AuditingMcpServerToolBindingTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "AddThroneTool: nullable IReadOnlyList<string>? = null биндится из JsonElement(Array) (regression: create_intent.tags)")]
    public async Task Binding_passes_nullable_string_array_argument()
    {
        using var provider = BuildProvider();
        var capture = provider.GetRequiredService<TagsCaptureTool>();
        var tool = provider.GetRequiredService<McpServerTool>();

        var result = await tool.InvokeAsync(
            NewCallContext("capture_tags", new Dictionary<string, JsonElement>
            {
                ["text"] = JsonDocument.Parse("\"hi\"").RootElement,
                ["tags"] = JsonDocument.Parse("[\"throne\",\"second\"]").RootElement,
            }),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        capture.LastTags.Should().NotBeNull();
        capture.LastTags!.Should().Equal("throne", "second");
    }

    [Fact(DisplayName = "AddThroneTool: tags пришёл как JSON-encoded строка (harness/relay-quirk) — биндится в массив")]
    public async Task Binding_unwraps_json_encoded_string_for_collection_argument()
    {
        using var provider = BuildProvider();
        var capture = provider.GetRequiredService<TagsCaptureTool>();
        var tool = provider.GetRequiredService<McpServerTool>();

        // Сериализуем JSON-массив как строку и заворачиваем в JsonElement(String) — это то,
        // что присылает remote-MCP relay, когда схема параметра не имеет `type: "array"`.
        var encoded = JsonDocument.Parse("\"[\\\"throne\\\"]\"").RootElement;
        var result = await tool.InvokeAsync(
            NewCallContext("capture_tags", new Dictionary<string, JsonElement>
            {
                ["text"] = JsonDocument.Parse("\"hi\"").RootElement,
                ["tags"] = encoded,
            }),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        capture.LastTags.Should().NotBeNull();
        capture.LastTags!.Should().Equal("throne");
    }

    [Fact(DisplayName = "AddThroneTool: nullable IReadOnlyList<string>? отсутствующий в аргументах остаётся null")]
    public async Task Binding_keeps_omitted_nullable_collection_as_default()
    {
        using var provider = BuildProvider();
        var capture = provider.GetRequiredService<TagsCaptureTool>();
        var tool = provider.GetRequiredService<McpServerTool>();

        var result = await tool.InvokeAsync(
            NewCallContext("capture_tags", new Dictionary<string, JsonElement>
            {
                ["text"] = JsonDocument.Parse("\"hi\"").RootElement,
            }),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        capture.LastTags.Should().BeNull();
    }

    [Fact(DisplayName = "AuditingMcpServerTool отдаёт клиенту реальный ex.Message в internal_error")]
    public async Task Internal_error_surfaces_real_exception_message_to_client()
    {
        using var provider = BuildProvider();
        var capture = provider.GetRequiredService<TagsCaptureTool>();
        capture.NextInvocationThrows = new InvalidOperationException("repro: handler exploded");
        var tool = provider.GetRequiredService<McpServerTool>();

        var result = await tool.InvokeAsync(
            NewCallContext("capture_tags", new Dictionary<string, JsonElement>
            {
                ["text"] = JsonDocument.Parse("\"hi\"").RootElement,
            }),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        var message = result.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        message.Should().NotBeNull();
        message.Should().Contain("repro: handler exploded");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMcpCallLogSink>(Substitute.For<IMcpCallLogSink>());
        services.AddSingleton<ICurrentUserAccessor>(new StubCurrentUser(CurrentUserIds.LocalDev));
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(new ServerVersion("test"));
        services.AddThroneTool<TagsCaptureTool>();
        return services.BuildServiceProvider();
    }

    private static RequestContext<CallToolRequestParams> NewCallContext(
        string toolName,
        IDictionary<string, JsonElement> arguments)
    {
        var server = Substitute.For<McpServer>();
        server.SessionId.Returns("session-bind");
        var jsonRpc = new JsonRpcRequest { Method = "tools/call", JsonRpc = "2.0", Id = new RequestId("1") };
        return new RequestContext<CallToolRequestParams>(server, jsonRpc, new CallToolRequestParams
        {
            Name = toolName,
            Arguments = arguments,
        });
    }

    private sealed class StubCurrentUser(string userId) : ICurrentUserAccessor
    {
        public string UserId { get; } = userId;
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Mirrors IntentTools.CreateIntent's parameter shape: nullable IReadOnlyList<string>? defaults to null.
    [McpServerToolType]
    internal sealed class TagsCaptureTool
    {
        public IReadOnlyList<string>? LastTags { get; private set; }
        public string? LastText { get; private set; }
        public Exception? NextInvocationThrows { get; set; }

        [McpServerTool(Name = "capture_tags", UseStructuredContent = true)]
        public Task<TagsCaptureResult> Capture(
            string text,
            IReadOnlyList<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            if (NextInvocationThrows is { } pending)
            {
                NextInvocationThrows = null;
                throw pending;
            }
            LastText = text;
            LastTags = tags;
            return Task.FromResult(new TagsCaptureResult(text, tags ?? Array.Empty<string>()));
        }
    }

    internal sealed record TagsCaptureResult(string Text, IReadOnlyList<string> Tags);
}
