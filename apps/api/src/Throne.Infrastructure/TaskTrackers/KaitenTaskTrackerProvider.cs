using Throne.Application.TaskTrackers;

namespace Throne.Infrastructure.TaskTrackers;

/// <summary>
/// The Kaiten entry in the task-tracker axis catalog (ADR-0045/0046) — the identity half of the
/// adapter. Its registration is the single line that makes <c>kaiten</c> appear in the catalog and
/// resolve through the provider registry; the read/write behaviour lives in the native HTTP client
/// (<see cref="Kaiten.IKaitenClient"/>), which a later axis port will surface through this provider.
/// </summary>
internal sealed class KaitenTaskTrackerProvider : ITaskTrackerProvider
{
    public string TrackerKey => "kaiten";

    public string DisplayName => "Kaiten";
}
