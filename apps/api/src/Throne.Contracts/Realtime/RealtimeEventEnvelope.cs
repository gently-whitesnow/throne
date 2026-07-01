namespace Throne.Realtime.Contracts;

/// <summary>
/// Transport-level envelope for a realtime event delivered over SSE.
/// Name must come from <c>RealtimeEventNames</c> (generated from events.yaml);
/// Payload is the JSON-serializable DTO referenced by the same yaml entry.
/// </summary>
public sealed record RealtimeEventEnvelope(string Name, object Payload);
