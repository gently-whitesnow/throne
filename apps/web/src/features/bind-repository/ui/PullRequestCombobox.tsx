import { ChevronDown, Loader2 } from "lucide-react";
import { useCallback, useEffect, useId, useRef, useState } from "react";

import {
  type GitProvider,
  type GitPullRequestRef,
  listGitProviderRepositoryPullRequests
} from "@/entities/repository-binding";

import { useRefSuggestions } from "../model/use-ref-suggestions";

interface PullRequestComboboxProps {
  provider: GitProvider;
  owner: string | null;
  repo: string | null;
  /** Free-text PR number (operator may type without selecting from the list). */
  value: string;
  /** Selected PR from suggestions, when picked from the dropdown. */
  selected: GitPullRequestRef | null;
  onValueChange: (value: string) => void;
  onSelect: (pr: GitPullRequestRef | null) => void;
  disabled: boolean;
  invalid: boolean;
}

/**
 * Typeahead for open pull requests of `{owner}/{repo}`. Selecting an item
 * propagates the full `GitPullRequestRef` (the modal uses `head_ref` to lock
 * the branch field per the slice spec).
 */
export function PullRequestCombobox({
  provider,
  owner,
  repo,
  value,
  selected,
  onValueChange,
  onSelect,
  disabled,
  invalid
}: PullRequestComboboxProps) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const inputId = useId();
  const wrapperRef = useRef<HTMLDivElement | null>(null);
  const enabled = owner !== null && repo !== null && !disabled;
  const fetcher = useCallback(
    (
      refOwner: string,
      refRepo: string,
      params: { q?: string; limit?: number },
      signal: AbortSignal
    ) =>
      listGitProviderRepositoryPullRequests(
        refOwner,
        refRepo,
        { ...params, provider },
        signal
      ),
    [provider]
  );
  const { results, isLoading } = useRefSuggestions<GitPullRequestRef>(
    owner,
    repo,
    query,
    enabled && open,
    fetcher
  );

  useEffect(() => {
    if (!open) return;
    const onDocClick = (e: MouseEvent) => {
      if (wrapperRef.current === null) return;
      if (!wrapperRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    window.addEventListener("mousedown", onDocClick);
    return () => {
      window.removeEventListener("mousedown", onDocClick);
    };
  }, [open]);

  function pick(pr: GitPullRequestRef) {
    // onSelect already propagates prNumber/branch through the parent's setter —
    // calling onValueChange here would spread a stale form snapshot and clobber
    // selectedPr/branch back to their previous values.
    onSelect(pr);
    setQuery("");
    setOpen(false);
  }

  return (
    <div className="relative flex flex-col gap-1 text-xs" ref={wrapperRef}>
      <label htmlFor={inputId} className="font-semibold text-base-content/70">
        PR (опционально)
      </label>
      {selected !== null ? (
        <div
          className="flex items-center justify-between gap-2 rounded-md border border-base-300 bg-base-200/60 px-2 py-1.5 font-mono text-xs"
          data-testid="bind-repository-pr-selected"
        >
          <span>
            <span className="font-semibold">#{selected.number}</span>{" "}
            <span className="text-base-content/70">— {selected.title}</span>
          </span>
          <button
            type="button"
            className="btn btn-ghost btn-xs"
            onClick={() => {
              onSelect(null);
            }}
            disabled={disabled}
            data-testid="bind-repository-pr-clear"
          >
            Открепить
          </button>
        </div>
      ) : (
        <div className="relative">
          <input
            id={inputId}
            type="text"
            inputMode="numeric"
            className={`input input-sm w-full pr-7 font-mono text-xs ${
              invalid ? "input-bordered border-error" : "input-bordered"
            }`}
            value={value || query}
            placeholder="например 1234 или текст из PR"
            disabled={!enabled}
            aria-invalid={invalid}
            aria-expanded={open}
            data-testid="bind-repository-pr-number"
            onFocus={() => {
              setOpen(true);
            }}
            onChange={(e) => {
              const v = e.target.value;
              setQuery(v);
              onValueChange(v);
              setOpen(true);
            }}
          />
          <span className="pointer-events-none absolute inset-y-0 right-1 flex items-center">
            {isLoading ? (
              <Loader2
                aria-hidden
                size={14}
                className="animate-spin text-base-content/50"
              />
            ) : (
              <ChevronDown
                aria-hidden
                size={14}
                className="text-base-content/40"
              />
            )}
          </span>
        </div>
      )}
      {open && selected === null && enabled ? (
        <ul
          className="absolute left-0 right-0 top-full z-20 mt-1 max-h-56 overflow-y-auto rounded-md border border-base-300 bg-base-100 shadow-lg"
          role="listbox"
          data-testid="bind-repository-pr-list"
        >
          {results.length === 0 && !isLoading ? (
            <li className="px-3 py-2 text-xs text-base-content/50">
              Открытых PR не найдено.
            </li>
          ) : null}
          {results.map((pr) => (
            <li key={pr.number}>
              <button
                type="button"
                className="block w-full px-3 py-1.5 text-left text-xs hover:bg-base-200"
                onMouseDown={(e) => {
                  e.preventDefault();
                  pick(pr);
                }}
                data-testid={`bind-repository-pr-row-${String(pr.number)}`}
              >
                <span className="font-mono font-semibold">#{pr.number}</span>{" "}
                <span className="text-base-content/80">— {pr.title}</span>
                <div className="font-mono text-[10px] text-base-content/50">
                  {pr.head_ref}
                </div>
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
