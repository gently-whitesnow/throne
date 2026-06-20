using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal static class MongoIntentLinkProjection
{
    public static async Task<List<IntentLinkView>> ProjectAsync(
        IMongoCollection<IntentDocument> intents,
        IClientSessionHandle? session,
        IntentId intentId,
        List<IntentLinkDocument> docs,
        CancellationToken ct)
    {
        if (docs.Count == 0)
        {
            return [];
        }

        var peerIds = CollectPeerIds(intentId, docs);
        var peersById = await LoadPeersAsync(intents, session, peerIds, ct);
        var result = new List<IntentLinkView>(docs.Count);
        foreach (var doc in docs)
        {
            var direction = string.Equals(doc.FromId, intentId.Value, StringComparison.Ordinal)
                ? IntentLinkDirection.Outgoing
                : IntentLinkDirection.Incoming;
            var peerId = direction == IntentLinkDirection.Outgoing ? doc.ToId : doc.FromId;
            if (peersById.TryGetValue(peerId, out var peer))
            {
                result.Add(new IntentLinkView(
                    MongoIntentLinkMapper.ToDomain(doc),
                    direction,
                    MongoIntentLinkMapper.IntentToDomain(peer)));
            }
        }
        return result;
    }

    private static HashSet<string> CollectPeerIds(IntentId intentId, IReadOnlyList<IntentLinkDocument> docs)
    {
        var peerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var doc in docs)
        {
            peerIds.Add(string.Equals(doc.FromId, intentId.Value, StringComparison.Ordinal) ? doc.ToId : doc.FromId);
        }
        return peerIds;
    }

    public static async Task<Dictionary<string, IntentDocument>> LoadPeersAsync(
        IMongoCollection<IntentDocument> intents,
        IClientSessionHandle? session,
        HashSet<string> peerIds,
        CancellationToken ct)
    {
        var peerFilter = Builders<IntentDocument>.Filter.In(d => d.Id, peerIds);
        var find = session is null ? intents.Find(peerFilter) : intents.Find(session, peerFilter);
        var peers = await find.ToListAsync(ct);
        return peers.ToDictionary(p => p.Id, p => p, StringComparer.Ordinal);
    }
}
