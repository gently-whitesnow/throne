using MongoDB.Driver;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Intents;

internal static class IntentCollectionFilters
{
    public static FilterDefinition<IntentDocument> ById(string id) =>
        Builders<IntentDocument>.Filter.Eq(d => d.Id, id);

    public static FilterDefinition<IntentDocument> BuildStatusUpdateFilter(
        string id,
        int currentVersion,
        string originalStatus)
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
            statusFilter);
    }
}
