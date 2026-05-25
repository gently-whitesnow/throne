import { ChevronDown, Loader2, Lock } from "lucide-react";
import { useEffect, useId, useRef, useState, type ReactNode } from "react";

import {
  type GitBranchRef,
  listGithubRepositoryBranches
} from "@/entities/repository-binding";

import { useRefSuggestions } from "../model/use-ref-suggestions";

interface BranchComboboxProps {
  owner: string | null;
  repo: string | null;
  value: string;
  onValueChange: (value: string) => void;
  disabled: boolean;
  /** Read-only mode: Branch is dictated by an external source (e.g. PR head ref). */
  locked: boolean;
  lockedHint?: ReactNode;
}

const fetcher = listGithubRepositoryBranches;

/**
 * Typeahead for branches of `{owner}/{repo}`. Free-text fallback is allowed —
 * the operator may clone a branch that exists locally but is not yet pushed.
 * When `locked=true` the field is rendered read-only with an explanatory hint
 * (used when a PR is selected — see `PullRequestCombobox`).
 */
export function BranchCombobox({
  owner,
  repo,
  value,
  onValueChange,
  disabled,
  locked,
  lockedHint
}: BranchComboboxProps) {
  const [open, setOpen] = useState(false);
  const inputId = useId();
  const wrapperRef = useRef<HTMLDivElement | null>(null);
  const enabled = owner !== null && repo !== null && !disabled && !locked;
  const { results, isLoading } = useRefSuggestions<GitBranchRef>(
    owner,
    repo,
    value,
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

  function pick(branch: GitBranchRef) {
    onValueChange(branch.name);
    setOpen(false);
  }

  return (
    <div className="relative flex flex-col gap-1 text-xs" ref={wrapperRef}>
      <label htmlFor={inputId} className="font-semibold text-base-content/70">
        Branch
      </label>
      <div className="relative">
        <input
          id={inputId}
          type="text"
          className="input input-bordered input-sm w-full pr-7 font-mono text-xs"
          value={value}
          disabled={disabled || locked}
          readOnly={locked}
          aria-readonly={locked}
          aria-expanded={open}
          data-testid="bind-repository-branch"
          onFocus={() => {
            if (!locked) setOpen(true);
          }}
          onChange={(e) => {
            onValueChange(e.target.value);
            setOpen(true);
          }}
        />
        <span className="pointer-events-none absolute inset-y-0 right-1 flex items-center">
          {locked ? (
            <Lock aria-hidden size={12} className="text-base-content/50" />
          ) : isLoading ? (
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
      {locked ? (
        <span
          className="text-base-content/60"
          data-testid="bind-repository-branch-locked-hint"
        >
          {lockedHint}
        </span>
      ) : null}
      {open && enabled ? (
        <ul
          className="absolute left-0 right-0 top-full z-20 mt-1 max-h-56 overflow-y-auto rounded-md border border-base-300 bg-base-100 shadow-lg"
          role="listbox"
          data-testid="bind-repository-branch-list"
        >
          {results.length === 0 && !isLoading ? (
            <li className="px-3 py-2 text-xs text-base-content/50">
              Веток не найдено.
            </li>
          ) : null}
          {results.map((branch) => (
            <li key={branch.name}>
              <button
                type="button"
                className="block w-full px-3 py-1.5 text-left text-xs hover:bg-base-200"
                onMouseDown={(e) => {
                  e.preventDefault();
                  pick(branch);
                }}
                data-testid={`bind-repository-branch-row-${branch.name}`}
              >
                <span className="font-mono">{branch.name}</span>
                {branch.is_default ? (
                  <span className="ml-2 rounded bg-base-200 px-1 text-[10px] uppercase tracking-wider text-base-content/60">
                    default
                  </span>
                ) : null}
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
