import { useCallback, useState } from "react";

import type { PickerSelection } from "./picker-types";
import { refKey } from "./repo-key";
import type { GitRepositoryRef } from "./types";

export interface AddManualResult {
  ok: boolean;
  reason?: string;
}

export interface PickerSelectionsApi {
  selections: PickerSelection[];
  has: (key: string) => boolean;
  /** Add a search repo, or remove it if already selected (chip toggle). */
  toggleSearch: (ref: GitRepositoryRef) => void;
  addManual: (ref: GitRepositoryRef) => AddManualResult;
  remove: (key: string) => void;
  reset: () => void;
}

/**
 * Default selection model для repository-picker: только ref + источник.
 * Используется потребителями (тег), у которых нет дополнительной per-chip
 * метаданных — bind-репозитория интента строит собственный аналог поверх тех же
 * правил dedup и `refKey`.
 */
export function usePickerSelections(): PickerSelectionsApi {
  const [selections, setSelections] = useState<PickerSelection[]>([]);

  const has = useCallback(
    (key: string) => selections.some((s) => s.key === key),
    [selections]
  );

  const toggleSearch = useCallback((ref: GitRepositoryRef) => {
    const key = refKey(ref);
    setSelections((prev) =>
      prev.some((s) => s.key === key)
        ? prev.filter((s) => s.key !== key)
        : [...prev, { key, source: "search", ref }]
    );
  }, []);

  const addManual = useCallback(
    (ref: GitRepositoryRef): AddManualResult => {
      const key = refKey(ref);
      if (selections.some((s) => s.key === key)) {
        return { ok: false, reason: "Этот репозиторий уже в списке." };
      }
      setSelections((prev) => [...prev, { key, source: "manual", ref }]);
      return { ok: true };
    },
    [selections]
  );

  const remove = useCallback((key: string) => {
    setSelections((prev) => prev.filter((s) => s.key !== key));
  }, []);

  const reset = useCallback(() => {
    setSelections([]);
  }, []);

  return { selections, has, toggleSearch, addManual, remove, reset };
}
