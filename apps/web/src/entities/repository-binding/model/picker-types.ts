import type { GitRepositoryRef } from "./types";

/** Where a picked repo came from: the autocomplete or a hand-pasted SSH URL. */
export type PickerSelectionSource = "search" | "manual";

/** Minimal shape a picker selection must carry. Consumers may extend it. */
export interface PickerSelection {
  /** Stable identity for dedupe + React keys (см. `refKey`). */
  key: string;
  source: PickerSelectionSource;
  ref: GitRepositoryRef;
}
