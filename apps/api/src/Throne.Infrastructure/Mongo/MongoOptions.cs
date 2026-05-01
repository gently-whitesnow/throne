namespace Throne.Infrastructure.Mongo;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    /// <summary>
    /// Mongo connection string. Должен указывать на replica set: write-tools используют
    /// multi-document transactions (см. ADR-0002 §6). Локально:
    /// <c>mongodb://localhost:27017/?replicaSet=rs0&amp;directConnection=true</c>.
    /// </summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true";

    public string Database { get; set; } = "throne";
}
