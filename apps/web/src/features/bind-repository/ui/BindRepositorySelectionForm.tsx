import type {
  GitPullRequestRef,
  GitRepositoryRef
} from "@/entities/repository-binding";

import { parsePrNumber, type PrNumberParse } from "../model/pr-number";
import { BranchCombobox } from "./BranchCombobox";
import { PullRequestCombobox } from "./PullRequestCombobox";

export interface BindRepositoryFormState {
  prNumber: string;
  selectedPr: GitPullRequestRef | null;
  branch: string;
}

interface BindRepositorySelectionFormProps {
  selected: GitRepositoryRef | null;
  form: BindRepositoryFormState;
  onChange: (next: BindRepositoryFormState) => void;
  disabled: boolean;
}

/**
 * PR + Branch editor for the selected repository. Layout puts PR first: if the
 * operator picks an open PR from suggestions, Branch is locked to that PR's
 * head ref. If no PR is selected, Branch is editable (combobox + free-text
 * fallback) and defaults to the upstream default branch.
 */
export function BindRepositorySelectionForm({
  selected,
  form,
  onChange,
  disabled
}: BindRepositorySelectionFormProps) {
  if (selected === null) return null;
  const repo = selected;
  const prNumberHint: PrNumberParse = parsePrNumber(form.prNumber);
  const prInvalid = prNumberHint.kind === "invalid";
  const branchLocked = form.selectedPr !== null;

  function setPrSelected(pr: GitPullRequestRef | null) {
    if (pr !== null) {
      onChange({
        ...form,
        selectedPr: pr,
        prNumber: String(pr.number),
        branch: pr.head_ref
      });
    } else {
      onChange({
        ...form,
        selectedPr: null,
        prNumber: "",
        branch: repo.default_branch
      });
    }
  }

  function setPrNumber(value: string) {
    onChange({ ...form, prNumber: value });
  }

  function setBranch(value: string) {
    onChange({ ...form, branch: value });
  }

  return (
    <div className="flex flex-col gap-3 rounded-md border border-base-200 bg-base-200/40 p-3">
      <p className="m-0 text-xs text-base-content/60">
        Выбрано:{" "}
        <span className="font-mono font-semibold text-base-content">
          {selected.full_name}
        </span>
      </p>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <PullRequestCombobox
          provider={selected.provider}
          owner={selected.owner}
          repo={selected.repo}
          value={form.prNumber}
          selected={form.selectedPr}
          onValueChange={setPrNumber}
          onSelect={setPrSelected}
          disabled={disabled}
          invalid={prInvalid}
        />
        <BranchCombobox
          provider={selected.provider}
          owner={selected.owner}
          repo={selected.repo}
          value={form.branch}
          onValueChange={setBranch}
          disabled={disabled}
          locked={branchLocked}
          lockedHint={
            form.selectedPr !== null
              ? `из PR #${String(form.selectedPr.number)}`
              : null
          }
        />
      </div>
      {prInvalid ? (
        <span className="text-xs text-error">
          PR number — целое число больше нуля.
        </span>
      ) : (
        <span className="text-xs text-base-content/50">
          Если PR не указан — клонируется ветка без отслеживания пул-реквеста.
        </span>
      )}
    </div>
  );
}
