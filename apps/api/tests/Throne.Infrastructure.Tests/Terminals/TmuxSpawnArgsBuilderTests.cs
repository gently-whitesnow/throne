using FluentAssertions;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

/// <summary>
/// Geometry handling of the tmux spawn argv: a valid client viewport becomes
/// <c>new-session -x/-y</c> (so the agent renders at the final size and the first attach resize
/// is a no-op); an absent or out-of-range dimension falls back to tmux's default 80×24.
/// </summary>
public class TmuxSpawnArgsBuilderTests
{
    private static TmuxSpawnRequest Request(int? cols, int? rows) =>
        new("intent-abc", "/ws/intent-abc", "claude", [], Cols: cols, Rows: rows);

    [Fact(DisplayName = "Build добавляет -x/-y, когда заданы валидные cols/rows")]
    public void Build_appends_geometry_when_valid()
    {
        var args = TmuxSpawnArgsBuilder.Build("throne-intent-abc", Request(135, 34));

        args.Should().ContainInOrder("-d", "-x", "135", "-y", "34", "claude");
    }

    [Theory(DisplayName = "Build не добавляет геометрию вне диапазона или с дырой")]
    [InlineData(null, 34)]
    [InlineData(135, null)]
    [InlineData(0, 34)]
    [InlineData(135, 1001)]
    public void Build_omits_geometry_when_invalid(int? cols, int? rows)
    {
        var args = TmuxSpawnArgsBuilder.Build("throne-intent-abc", Request(cols, rows));

        args.Should().NotContain("-x").And.NotContain("-y");
    }
}
