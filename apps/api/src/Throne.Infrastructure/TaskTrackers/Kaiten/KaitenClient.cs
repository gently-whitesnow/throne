namespace Throne.Infrastructure.TaskTrackers.Kaiten;

/// <summary>
/// Composes the per-resource endpoint groups into the single <see cref="IKaitenClient"/> handle.
/// Holds no transport state itself — the shared <c>KaitenHttpExecutor</c> behind each group owns the
/// rate limit and retries — so this is just the discovery surface for the adapter.
/// </summary>
internal sealed class KaitenClient(
    IKaitenTopologyApi topology,
    IKaitenCardsApi cards,
    IKaitenCommentsApi comments,
    IKaitenTagsApi tags,
    IKaitenCardChildrenApi cardChildren) : IKaitenClient
{
    public IKaitenTopologyApi Topology => topology;

    public IKaitenCardsApi Cards => cards;

    public IKaitenCommentsApi Comments => comments;

    public IKaitenTagsApi Tags => tags;

    public IKaitenCardChildrenApi CardChildren => cardChildren;
}
