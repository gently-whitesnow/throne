using FluentAssertions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Tests.Git;

public sealed class LocalGitBlobReaderTests
{
    [Fact(DisplayName = "GetFileLinesAsync возвращает запрошенный диапазон и total_lines")]
    public async Task GetFileLinesAsyncReturnsRequestedRange()
    {
        var fx = new Fixture();
        fx.OnRun(req => Fixture.IsShow(req)
            ? Fixture.Ok("a\nb\nc\n")
            : Fixture.Fail());

        var slice = await fx.Reader.GetFileLinesAsync("/repo", "abc1234", "src/app.ts", 2, 10, CancellationToken.None);

        slice.From.Should().Be(2);
        slice.To.Should().Be(3);
        slice.TotalLines.Should().Be(3);
        slice.Lines.Should().Equal(
            new RepositoryFileLine(2, "b"),
            new RepositoryFileLine(3, "c"));
        fx.Calls.Should().ContainSingle(call => Fixture.IsShow(call));
    }

    [Fact(DisplayName = "GetFileLinesAsync при первом промахе fetch'ит sha и повторяет чтение")]
    public async Task GetFileLinesAsyncFetchesMissingObjectThenReadsAgain()
    {
        var fx = new Fixture();
        var showCalls = 0;
        fx.OnRun(req =>
        {
            if (Fixture.IsFetch(req))
            {
                return Fixture.Ok("");
            }

            showCalls += 1;
            return showCalls == 1 ? Fixture.Fail() : Fixture.Ok("one\ntwo\n");
        });

        var slice = await fx.Reader.GetFileLinesAsync("/repo", "def5678", "README.md", 1, 1, CancellationToken.None);

        slice.Lines.Should().Equal(new RepositoryFileLine(1, "one"));
        fx.Calls.Select(c => c.Arguments[2]).Should().Equal("show", "fetch", "show");
        fx.Calls.Single(Fixture.IsFetch).Arguments.Should().ContainInOrder("origin", "def5678");
        fx.Calls.Single(Fixture.IsFetch).Arguments.Should().NotContain("--filter=blob:none");
    }

    [Fact(DisplayName = "GetFileLinesAsync отдаёт типизированную ошибку если объект недоступен после fetch")]
    public async Task GetFileLinesAsyncThrowsTypedErrorWhenObjectStaysUnavailable()
    {
        var fx = new Fixture();
        fx.OnRun(_ => Fixture.Fail());

        var act = () => fx.Reader.GetFileLinesAsync("/repo", "badcafe", "missing.txt", 1, 20, CancellationToken.None);

        await act.Should().ThrowAsync<RepositoryBlobReadException>();
        fx.Calls.Should().Contain(call => Fixture.IsFetch(call));
    }

    [Fact(DisplayName = "GetFileLinesAsync пробрасывает stderr из неудачных git show и fetch")]
    public async Task GetFileLinesAsyncIncludesGitDiagnosticsInTypedError()
    {
        var fx = new Fixture();
        fx.OnRun(req => Fixture.IsFetch(req)
            ? Fixture.Fail(128, "fatal: couldn't find remote ref badcafe")
            : Fixture.Fail(128, "fatal: Not a valid object name badcafe:missing.txt"));

        var act = () => fx.Reader.GetFileLinesAsync("/repo", "badcafe", "missing.txt", 1, 20, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<RepositoryBlobReadException>();
        ex.Which.Message.Should().Contain("git fetch failed");
        ex.Which.Message.Should().Contain("exit 128: fatal: couldn't find remote ref badcafe");
        ex.Which.Message.Should().Contain("initial git show failed");
        ex.Which.Message.Should().Contain("fatal: Not a valid object name badcafe:missing.txt");
    }

    [Fact(DisplayName = "GetFileLinesAsync пробрасывает stderr повторного git show после успешного fetch")]
    public async Task GetFileLinesAsyncIncludesSecondShowDiagnosticsWhenFetchSucceeds()
    {
        var fx = new Fixture();
        var showCalls = 0;
        fx.OnRun(req =>
        {
            if (Fixture.IsFetch(req))
            {
                return Fixture.Ok("");
            }

            showCalls += 1;
            return showCalls == 1
                ? Fixture.Fail(128, "fatal: invalid object name")
                : Fixture.Fail(128, "fatal: path 'missing.txt' exists on disk, but not in 'badcafe'");
        });

        var act = () => fx.Reader.GetFileLinesAsync("/repo", "badcafe", "missing.txt", 1, 20, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<RepositoryBlobReadException>();
        ex.Which.Message.Should().Contain("git show failed after fetch");
        ex.Which.Message.Should().Contain("fatal: path 'missing.txt' exists on disk, but not in 'badcafe'");
    }

    private sealed class Fixture
    {
        private readonly IProcessLauncher _launcher = Substitute.For<IProcessLauncher>();

        public Fixture()
        {
            Reader = new LocalGitBlobReader(_launcher);
        }

        public LocalGitBlobReader Reader { get; }
        public List<ProcessRunRequest> Calls { get; } = [];

        public void OnRun(Func<ProcessRunRequest, ProcessRunResult> factory)
        {
            _launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var req = ci.Arg<ProcessRunRequest>();
                    Calls.Add(req);
                    return Task.FromResult(factory(req));
                });
        }

        public static bool IsShow(ProcessRunRequest req) =>
            req.FileName == "git" && req.Arguments.Contains("show");

        public static bool IsFetch(ProcessRunRequest req) =>
            req.FileName == "git" && req.Arguments.Contains("fetch");

        public static ProcessRunResult Ok(string stdout) =>
            new(0, stdout, string.Empty, TimeSpan.Zero);

        public static ProcessRunResult Fail() =>
            Fail(128, "fatal");

        public static ProcessRunResult Fail(int exit, string stderr) =>
            new(exit, string.Empty, stderr, TimeSpan.Zero);
    }
}
