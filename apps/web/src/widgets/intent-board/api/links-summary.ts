import type { IntentLinksSummaryEntry } from "@/entities/intent";
import {
  httpGet,
  intentsEndpoints,
  type IntentsComponents
} from "@/shared/api";

type IntentLinksSummaryDto =
  IntentsComponents["schemas"]["IntentLinksSummaryDto"];

/**
 * Batch-fetch link aggregates for the supplied intent ids in one round-trip.
 * Companion to `listIntents`: the list itself stays graph-free so list refresh
 * and links refresh invalidate independently. Server returns at most one entry
 * per id and omits ids without incident edges — callers default missing ids to
 * «no links».
 */
export async function fetchIntentLinksSummary(
  ids: readonly string[],
  signal?: AbortSignal
): Promise<IntentLinksSummaryEntry[]> {
  if (ids.length === 0) return [];
  const query = ids.map((id) => `ids=${encodeURIComponent(id)}`).join("&");
  const path = `${intentsEndpoints.getIntentLinksSummary()}?${query}`;
  const dto = await httpGet<IntentLinksSummaryDto>(path, signal);
  return dto.items;
}
