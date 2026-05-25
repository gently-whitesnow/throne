using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Tests.Git;

public class WorkspaceRootInitializerTests : IDisposable
{
    private readonly string _tempRoot;

    public WorkspaceRootInitializerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"throne-workspace-{Guid.NewGuid():N}");
    }

    [Fact(DisplayName = "StartAsync создаёт root рекурсивно и выставляет ResolvedRoot")]
    public async Task Creates_root_and_exposes_absolute_path()
    {
        var nested = Path.Combine(_tempRoot, "a", "b", "c");
        var initializer = NewInitializer(nested);

        await initializer.StartAsync(CancellationToken.None);

        Directory.Exists(nested).Should().BeTrue();
        initializer.ResolvedRoot.Should().Be(Path.GetFullPath(nested));
        // Probe file must not linger — it's only used to assert writability.
        Directory.EnumerateFiles(nested).Should().BeEmpty();
    }

    [Fact(DisplayName = "StartAsync разворачивает leading ~ в домашнюю директорию")]
    public async Task Expands_home_alias()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var relative = $".throne-test-{Guid.NewGuid():N}";
        var initializer = NewInitializer("~/" + relative);

        try
        {
            await initializer.StartAsync(CancellationToken.None);
            initializer.ResolvedRoot.Should().Be(Path.GetFullPath(Path.Combine(home, relative)));
        }
        finally
        {
            TryCleanup(Path.Combine(home, relative));
        }
    }

    [Fact(DisplayName = "StartAsync падает с InvalidOperationException если root не записываем")]
    public async Task Fails_when_root_not_writable()
    {
        // On Unix we can create a 0500 directory and the write-probe will throw.
        // On Windows file ACLs differ enough that we skip the negative test rather
        // than introduce P/Invoke just to assert one branch.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(_tempRoot);
        File.SetUnixFileMode(_tempRoot,
            UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            var initializer = NewInitializer(_tempRoot);

            var act = async () => await initializer.StartAsync(CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .Where(e => e.Message.Contains("not writable", StringComparison.Ordinal));
        }
        finally
        {
            // Restore permissions so cleanup in Dispose works.
            File.SetUnixFileMode(_tempRoot,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact(DisplayName = "StartAsync падает на пустом Root конфиге")]
    public async Task Fails_when_root_blank()
    {
        var initializer = NewInitializer("");

        var act = async () => await initializer.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains("Throne:Workspace:Root", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "ResolvedRoot бросает до StartAsync")]
    public void ResolvedRoot_throws_before_start()
    {
        var initializer = NewInitializer(_tempRoot);

        var act = () => initializer.ResolvedRoot;

        act.Should().Throw<InvalidOperationException>();
    }

    public void Dispose()
    {
        TryCleanup(_tempRoot);
        GC.SuppressFinalize(this);
    }

    private static void TryCleanup(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup — leftovers are noise, not a test failure.
        }
    }

    private static WorkspaceRootInitializer NewInitializer(string root) =>
        new(
            Options.Create(new WorkspaceOptions { Root = root }),
            NullLogger<WorkspaceRootInitializer>.Instance);
}
