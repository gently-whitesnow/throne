import { Link2, Loader2, X } from "lucide-react";
import { useId } from "react";

import {
  manualHostError,
  type GitPullRequestRef
} from "@/entities/repository-binding";

import { parsePrNumber } from "../model/pr-number";
import { type RepoSelection } from "../model/selection";
import { BranchCombobox } from "./BranchCombobox";
import { PullRequestCombobox } from "./PullRequestCombobox";

interface BindRepositoryChipProps {
  selection: RepoSelection;
  gitlabHost: string | null;
  disabled: boolean;
  onRemove: () => void;
  onBranch: (value: string) => void;
  onPrNumber: (value: string) => void;
  onSelectedPr: (pr: GitPullRequestRef | null) => void;
}

/**
 * One selected repository in the multi-select list, with its own branch/PR
 * editor. Search chips get the branch/PR typeaheads; manual (SSH) chips get
 * plain inputs because there is no API access to enumerate refs (slice spec).
 */
export function BindRepositoryChip({
  selection,
  gitlabHost,
  disabled,
  onRemove,
  onBranch,
  onPrNumber,
  onSelectedPr
}: BindRepositoryChipProps) {
  const { ref, source } = selection;
  const isManual = source === "manual";
  const hostError = isManual ? manualHostError(ref, gitlabHost) : null;
  const branchLocked = selection.selectedPr !== null;

  return (
    <div
      className="flex flex-col gap-2 rounded-md border border-base-300 bg-base-100 p-3"
      data-testid={`bind-repository-chip-${ref.full_name}`}
    >
      <div className="flex items-start justify-between gap-2">
        <span className="flex min-w-0 items-center gap-1.5 truncate font-mono text-sm font-semibold">
          {isManual ? (
            <Link2
              aria-label="SSH"
              size={12}
              className="text-base-content/50"
            />
          ) : null}
          {ref.full_name}
        </span>
        <div className="flex items-center gap-1">
          {selection.status === "binding" ? (
            <Loader2
              aria-hidden
              size={14}
              className="animate-spin text-base-content/50"
            />
          ) : null}
          <button
            type="button"
            className="btn btn-ghost btn-xs btn-circle"
            onClick={onRemove}
            disabled={disabled}
            aria-label={`Убрать ${ref.full_name}`}
            data-testid={`bind-repository-chip-remove-${ref.full_name}`}
          >
            <X aria-hidden size={14} strokeWidth={2} />
          </button>
        </div>
      </div>

      {isManual ? (
        <ManualRefEditor
          selection={selection}
          disabled={disabled}
          onBranch={onBranch}
          onPrNumber={onPrNumber}
        />
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <PullRequestCombobox
            provider={ref.provider}
            owner={ref.owner}
            repo={ref.repo}
            value={selection.prNumber}
            selected={selection.selectedPr}
            onValueChange={onPrNumber}
            onSelect={onSelectedPr}
            disabled={disabled}
            invalid={parsePrNumber(selection.prNumber).kind === "invalid"}
          />
          <BranchCombobox
            provider={ref.provider}
            owner={ref.owner}
            repo={ref.repo}
            value={selection.branch}
            onValueChange={onBranch}
            disabled={disabled}
            locked={branchLocked}
            lockedHint={
              selection.selectedPr !== null
                ? `из PR #${String(selection.selectedPr.number)}`
                : null
            }
          />
        </div>
      )}

      {hostError !== null ? (
        <span
          className="text-xs text-error"
          data-testid={`bind-repository-chip-host-error-${ref.full_name}`}
        >
          {hostError}
        </span>
      ) : null}
      {selection.error !== null ? (
        <span
          role="alert"
          className="text-xs text-error"
          data-testid={`bind-repository-chip-error-${ref.full_name}`}
        >
          {selection.error}
        </span>
      ) : null}
    </div>
  );
}

interface ManualRefEditorProps {
  selection: RepoSelection;
  disabled: boolean;
  onBranch: (value: string) => void;
  onPrNumber: (value: string) => void;
}

function ManualRefEditor({
  selection,
  disabled,
  onBranch,
  onPrNumber
}: ManualRefEditorProps) {
  const branchId = useId();
  const prId = useId();
  const prInvalid = parsePrNumber(selection.prNumber).kind === "invalid";

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
      <div className="flex flex-col gap-1 text-xs">
        <label htmlFor={prId} className="font-semibold text-base-content/70">
          PR (опционально)
        </label>
        <input
          id={prId}
          type="text"
          inputMode="numeric"
          className={`input input-sm w-full font-mono text-xs ${
            prInvalid ? "input-bordered border-error" : "input-bordered"
          }`}
          value={selection.prNumber}
          placeholder="например 1234"
          disabled={disabled}
          aria-invalid={prInvalid}
          data-testid={`bind-repository-chip-pr-${selection.ref.full_name}`}
          onChange={(e) => {
            onPrNumber(e.target.value);
          }}
        />
      </div>
      <div className="flex flex-col gap-1 text-xs">
        <label
          htmlFor={branchId}
          className="font-semibold text-base-content/70"
        >
          Branch
        </label>
        <input
          id={branchId}
          type="text"
          className="input input-bordered input-sm w-full font-mono text-xs"
          value={selection.branch}
          placeholder="по умолчанию (main)"
          disabled={disabled}
          data-testid={`bind-repository-chip-branch-${selection.ref.full_name}`}
          onChange={(e) => {
            onBranch(e.target.value);
          }}
        />
      </div>
    </div>
  );
}
