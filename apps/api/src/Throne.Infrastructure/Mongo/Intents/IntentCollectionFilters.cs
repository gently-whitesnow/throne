using MongoDB.Driver;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Intents;

internal static class IntentCollectionFilters
{
    public static FilterDefinition<IntentDocument> Owner(string userId) =>
        Builders<IntentDocument>.Filter.Eq(d => d.OwnerUserId, userId);

    public static FilterDefinition<IntentDocument> ByIdAndOwner(string id, string userId) =>
        Builders<IntentDocument>.Filter.And(
            Builders<IntentDocument>.Filter.Eq(d => d.Id, id),
            Owner(userId));

    public static FilterDefinition<IntentDocument> BuildStatusUpdateFilter(
        string id,
        int currentVersion,
        string originalStatus,
        string ownerUserId)
    {
        var filter = Builders<IntentDocument>.Filter;
        var statusFilter = filter.Eq(d => d.Status, originalStatus);
        if (string.Equals(originalStatus, IntentStatusNames.Draft, StringComparison.Ordinal))
        {
            statusFilter = filter.Or(statusFilter, filter.Eq(d => d.Status, string.Empty));
        }

        return filter.And(
            filter.Eq(d => d.Id, id),
            filter.Eq(d => d.CurrentVersion, currentVersion),
            statusFilter,
            Owner(ownerUserId));
    }
}
