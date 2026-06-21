import { useCallback, useState } from "react";

import {
  bindIntentRepository,
  type BindRepositoryRequest,
  type GitPullRequestRef,
  type GitRepositoryRef,
  type RepositoryBinding
} from "@/entities/repository-binding";
import { errorMessage } from "@/shared/lib";

import { parsePrNumber } from "./pr-number";
import {
  createManualSelection,
  createSearchSelection,
  refKey,
  selectionIssue,
  type RepoSelection
} from "./selection";

export interface AddManualResult {
  ok: boolean;
  reason?: string;
}

export interface BindSelectionsApi {
  selections: RepoSelection[];
  busy: boolean;
  has: (key: string) => boolean;
  /** Add a search repo, or remove it if already selected (chip toggle). */
  toggleSearch: (ref: GitRepositoryRef) => void;
  addManual: (ref: GitRepositoryRef) => AddManualResult;
  remove: (key: string) => void;
  setBranch: (key: string, branch: string) => void;
  setPrNumber: (key: string, value: string) => void;
  setSelectedPr: (key: string, pr: GitPullRequestRef | null) => void;
  reset: () => void;
  /**
   * Bind every chip without a blocking issue in parallel. Succeeded chips are
   * dropped; failed ones keep their server error so the operator can retry.
   * Returns `true` when nothing is left (caller closes the modal).
   */
  submit: (
    gitlabHost: string | null,
    onBound?: (binding: RepositoryBinding) => void
  ) => Promise<boolean>;
}

function toRequest(selection: RepoSelection): BindRepositoryRequest {
  const pr = parsePrNumber(selection.prNumber);
  const branch = selection.branch.trim();
  return {
    provider: selection.ref.provider,
    host: selection.ref.host,
    owner: selection.ref.owner,
    repo: selection.ref.repo,
    project_id: selection.ref.project_id,
    default_branch: branch.length > 0 ? branch : undefined,
    pull_request_number: pr.kind === "value" ? pr.value : undefined
  };
}

function formatBindError(err: unknown): string {
  return errorMessage(err, {
    base: "Не удалось привязать",
    byStatus: {
      409: "Уже привязан к интенту.",
      422: "Сервер отклонил параметры (422). Проверьте provider и owner/repo.",
      404: "Интент не найден (404)."
    },
    fallback: "Не удалось привязать репозиторий."
  });
}

export function useBindSelections(intentId: string): BindSelectionsApi {
  const [selections, setSelections] = useState<RepoSelection[]>([]);
  const [busy, setBusy] = useState(false);

  const has = useCallback(
    (key: string) => selections.some((s) => s.key === key),
    [selections]
  );

  const patch = useCallback(
    (key: string, updater: (prev: RepoSelection) => RepoSelection) => {
      setSelections((prev) =>
        prev.map((s) => (s.key === key ? updater(s) : s))
      );
    },
    []
  );

  const toggleSearch = useCallback((ref: GitRepositoryRef) => {
    const key = refKey(ref);
    setSelections((prev) =>
      prev.some((s) => s.key === key)
        ? prev.filter((s) => s.key !== key)
        : [...prev, createSearchSelection(ref)]
    );
  }, []);

  const addManual = useCallback(
    (ref: GitRepositoryRef): AddManualResult => {
      const key = refKey(ref);
      if (selections.some((s) => s.key === key)) {
        return { ok: false, reason: "Этот репозиторий уже в списке." };
      }
      setSelections((prev) => [...prev, createManualSelection(ref)]);
      return { ok: true };
    },
    [selections]
  );

  const remove = useCallback((key: string) => {
    setSelections((prev) => prev.filter((s) => s.key !== key));
  }, []);

  const setBranch = useCallback(
    (key: string, branch: string) => {
      patch(key, (s) => ({ ...s, branch }));
    },
    [patch]
  );

  const setPrNumber = useCallback(
    (key: string, value: string) => {
      patch(key, (s) => ({ ...s, prNumber: value }));
    },
    [patch]
  );

  const setSelectedPr = useCallback(
    (key: string, pr: GitPullRequestRef | null) => {
      patch(key, (s) =>
        pr !== null
          ? {
              ...s,
              selectedPr: pr,
              prNumber: String(pr.number),
              branch: pr.head_ref
            }
          : {
              ...s,
              selectedPr: null,
              prNumber: "",
              branch: s.ref.default_branch
            }
      );
    },
    [patch]
  );

  const reset = useCallback(() => {
    setSelections([]);
    setBusy(false);
  }, []);

  const submit = useCallback(
    async (
      gitlabHost: string | null,
      onBound?: (binding: RepositoryBinding) => void
    ): Promise<boolean> => {
      const targets = selections.filter(
        (s) => selectionIssue(s, gitlabHost) === null
      );
      if (targets.length === 0) return false;

      const targetKeys = new Set(targets.map((s) => s.key));
      setBusy(true);
      setSelections((prev) =>
        prev.map((s) =>
          targetKeys.has(s.key) ? { ...s, status: "binding", error: null } : s
        )
      );

      const results = await Promise.allSettled(
        targets.map((s) => bindIntentRepository(intentId, toRequest(s)))
      );

      const succeeded = new Set<string>();
      const failures = new Map<string, string>();
      results.forEach((result, index) => {
        const key = targets[index].key;
        if (result.status === "fulfilled") {
          succeeded.add(key);
          onBound?.(result.value);
        } else {
          failures.set(key, formatBindError(result.reason));
        }
      });

      setSelections((prev) =>
        prev
          .filter((s) => !succeeded.has(s.key))
          .map((s) => {
            const failure = failures.get(s.key);
            return failure !== undefined
              ? { ...s, status: "error" as const, error: failure }
              : s;
          })
      );
      setBusy(false);
      // Chips left over = non-bindable (skipped) + failed binds. Computed from
      // the closure, not the setState updater, which has not run yet here.
      const remaining = selections.length - succeeded.size;
      return remaining === 0;
    },
    [intentId, selections]
  );

  return {
    selections,
    busy,
    has,
    toggleSearch,
    addManual,
    remove,
    setBranch,
    setPrNumber,
    setSelectedPr,
    reset,
    submit
  };
}
