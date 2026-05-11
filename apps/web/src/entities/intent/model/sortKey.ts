/**
 * Ordinal (byte-wise) comparator for fractional sort_key strings.
 *
 * The server stores keys in a base62 alphabet ('0..9A..Za..z') and orders them
 * with ordinal byte comparison. JavaScript's `String.localeCompare` is locale-aware
 * — under most locales it case-folds, so 'V' ≈ 'v' and 's' compares as less than 'V'.
 * That mismatch causes the rendered list to disagree with the server's order, which
 * inverts the (before_id, after_id) pivots a DnD computes and the move endpoint
 * rejects them with a 422 "Pivots are out of order".
 *
 * Use this comparator everywhere we sort intents by sort_key on the client.
 */
export function compareSortKeys(a: string, b: string): number {
  if (a < b) return -1;
  if (a > b) return 1;
  return 0;
}
