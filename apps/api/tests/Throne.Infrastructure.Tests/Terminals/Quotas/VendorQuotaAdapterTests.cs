using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals.Quotas;

namespace Throne.Infrastructure.Tests.Terminals.Quotas;

public class VendorQuotaAdapterTests
{
    [Fact(DisplayName = "Claude.Parse: маппит five_hour/seven_day + ISO 8601 сохраняется")]
    public void Parses_claude_usage()
    {
        var payload = /* lang=json */ """
            {
              "five_hour": { "used_percentage": 55.4, "resets_at": "2026-07-10T15:00:00Z" },
              "seven_day": { "used_percentage": 18.2, "resets_at": "2026-07-15T00:00:00Z" }
            }
            """;

        var snap = ClaudeQuotaAdapter.Parse(payload);

        snap.Should().NotBeNull();
        snap!.FiveHour.UsedPercent.Should().Be(55.4);
        snap.FiveHour.ResetsAt.Should().Be("2026-07-10T15:00:00Z");
        snap.SevenDay!.UsedPercent.Should().Be(18.2);
        snap.CreditsBalance.Should().BeNull();
    }

    [Fact(DisplayName = "Claude.Parse: клампит проценты и не падает без seven_day")]
    public void Clamps_claude_percent_and_allows_null_weekly()
    {
        var payload = /* lang=json */ """
            { "five_hour": { "used_percentage": 145, "resets_at": null } }
            """;

        var snap = ClaudeQuotaAdapter.Parse(payload);

        snap!.FiveHour.UsedPercent.Should().Be(100);
        snap.FiveHour.ResetsAt.Should().BeNull();
        snap.SevenDay.Should().BeNull();
    }

    [Fact(DisplayName = "Claude.Parse: пустая пятичасовая метрика → null-снапшот")]
    public void Rejects_claude_snapshot_when_five_hour_missing()
    {
        ClaudeQuotaAdapter.Parse("{}").Should().BeNull();
        ClaudeQuotaAdapter.Parse("""{"five_hour":{}}""").Should().BeNull();
    }

    [Fact(DisplayName = "Codex.Parse: маппит primary/secondary, Unix seconds → ISO 8601, credits.balance")]
    public void Parses_codex_usage()
    {
        var payload = /* lang=json */ """
            {
              "primary":   { "used_percent": 33.0, "resets_at": 1783011600 },
              "secondary": { "used_percent": 8.5,  "resets_at": null       },
              "credits":   { "balance": 6.42 }
            }
            """;

        var snap = CodexQuotaAdapter.Parse(payload);

        snap.Should().NotBeNull();
        var expectedFiveHourReset = DateTimeOffset.FromUnixTimeSeconds(1783011600)
            .UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

        snap!.FiveHour.UsedPercent.Should().Be(33.0);
        snap.FiveHour.ResetsAt.Should().Be(expectedFiveHourReset);
        snap.SevenDay!.UsedPercent.Should().Be(8.5);
        snap.SevenDay.ResetsAt.Should().BeNull();
        snap.CreditsBalance.Should().Be(6.42);
    }

    [Fact(DisplayName = "Codex.Parse: primary отсутствует → null-снапшот")]
    public void Rejects_codex_snapshot_without_primary()
    {
        CodexQuotaAdapter.Parse("""{ "credits": { "balance": 5 } }""").Should().BeNull();
    }

    [Fact(DisplayName = "Codex.ReadCredentials: разбирает tokens.access_token + account_id")]
    public void Reads_codex_credentials_from_disk()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, /* lang=json */ """
                {
                  "auth_mode": "chatgpt",
                  "tokens": {
                    "access_token": "abc",
                    "refresh_token": "xyz",
                    "id_token": "id",
                    "account_id": "acc-1"
                  },
                  "last_refresh": "2026-01-01T00:00:00Z"
                }
                """);

            var creds = CodexQuotaAdapter.ReadCredentials(path);

            creds.Should().NotBeNull();
            creds!.AccessToken.Should().Be("abc");
            creds.AccountId.Should().Be("acc-1");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "Claude.ReadAccessToken: файла нет → null")]
    public void Returns_null_when_claude_credentials_missing()
    {
        ClaudeQuotaAdapter.ReadAccessToken("/nowhere/nothing").Should().BeNull();
    }

    [Fact(DisplayName = "Base: проба бросает → null-снапшот, кэшируется до истечения TTL")]
    public async Task Swallows_exception_and_caches_null()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var adapter = new ThrowingAdapter(clock);

        (await adapter.ReadAsync(CancellationToken.None)).Should().BeNull();
        (await adapter.ReadAsync(CancellationToken.None)).Should().BeNull();
        adapter.Calls.Should().Be(1);

        clock.Advance(TimeSpan.FromSeconds(65));
        (await adapter.ReadAsync(CancellationToken.None)).Should().BeNull();
        adapter.Calls.Should().Be(2);
    }

    [Fact(DisplayName = "Base: результат кэшируется на 60с и переспрашивается после TTL")]
    public async Task Caches_snapshot_and_refreshes_after_ttl()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var window = new VendorQuotaWindow(50, null);
        var adapter = new CountingAdapter(
            clock,
            () => new VendorQuotaSnapshot(window, null, null));

        var first = await adapter.ReadAsync(CancellationToken.None);
        var second = await adapter.ReadAsync(CancellationToken.None);
        first.Should().BeSameAs(second);
        adapter.Calls.Should().Be(1);

        clock.Advance(TimeSpan.FromSeconds(65));
        await adapter.ReadAsync(CancellationToken.None);
        adapter.Calls.Should().Be(2);
    }

    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private sealed class ThrowingAdapter(TimeProvider clock)
        : HttpVendorQuotaAdapterBase(NullLogger.Instance, clock)
    {
        public int Calls { get; private set; }
        public override string Vendor => "throwing";
        protected override Task<VendorQuotaSnapshot?> ProbeAsync(CancellationToken ct)
        {
            Calls++;
            throw new InvalidOperationException("simulated");
        }
    }

    private sealed class CountingAdapter(TimeProvider clock, Func<VendorQuotaSnapshot?> factory)
        : HttpVendorQuotaAdapterBase(NullLogger.Instance, clock)
    {
        public int Calls { get; private set; }
        public override string Vendor => "counting";
        protected override Task<VendorQuotaSnapshot?> ProbeAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(factory());
        }
    }
}
