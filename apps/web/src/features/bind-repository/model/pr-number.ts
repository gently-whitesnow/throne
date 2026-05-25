/**
 * PR-number input parsing. Modal accepts a free-form text field because numeric
 * inputs misbehave on iOS / pasted values; this guards the wire payload.
 *
 *  - `empty` — operator left the field blank → no PR tracking.
 *  - `value` — positive integer to send as `pull_request_number`.
 *  - `invalid` — non-numeric or non-positive; submit must stay disabled.
 */
export type PrNumberParse =
  | { kind: "empty" }
  | { kind: "value"; value: number }
  | { kind: "invalid" };

export function parsePrNumber(raw: string): PrNumberParse {
  const trimmed = raw.trim();
  if (trimmed.length === 0) return { kind: "empty" };
  if (!/^\d+$/.test(trimmed)) return { kind: "invalid" };
  const value = Number(trimmed);
  if (!Number.isFinite(value) || value <= 0 || !Number.isInteger(value)) {
    return { kind: "invalid" };
  }
  return { kind: "value", value };
}
