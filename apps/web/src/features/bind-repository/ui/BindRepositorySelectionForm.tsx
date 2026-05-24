import type { GitRepositoryRef } from "@/entities/repository-binding";

import { parsePrNumber, type PrNumberParse } from "../model/pr-number";

export interface BindRepositoryFormState {
  branch: string;
  prNumber: string;
}

interface BindRepositorySelectionFormProps {
  selected: GitRepositoryRef | null;
  form: BindRepositoryFormState;
  onChange: (next: BindRepositoryFormState) => void;
  disabled: boolean;
}

/**
 * Branch + optional PR number editor for the selected repository. Hidden
 * until the user picks a repo so the modal stays focused on search-first UX.
 *
 * PR-number parsing lives in `model/pr-number` and is also re-used by the
 * parent modal for submit-gating.
 */
export function BindRepositorySelectionForm({
  selected,
  form,
  onChange,
  disabled
}: BindRepositorySelectionFormProps) {
  if (selected === null) return null;
  const prNumberHint: PrNumberParse = parsePrNumber(form.prNumber);
  const prInvalid = prNumberHint.kind === "invalid";

  return (
    <div className="flex flex-col gap-3 rounded-md border border-base-200 bg-base-200/40 p-3">
      <p className="m-0 text-xs text-base-content/60">
        Выбрано:{" "}
        <span className="font-mono font-semibold text-base-content">
          {selected.full_name}
        </span>
      </p>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="flex flex-col gap-1 text-xs">
          <span className="font-semibold text-base-content/70">Branch</span>
          <input
            type="text"
            className="input input-bordered input-sm w-full font-mono text-xs"
            value={form.branch}
            onChange={(e) => {
              onChange({ ...form, branch: e.target.value });
            }}
            disabled={disabled}
            aria-label="Branch для клонирования"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          <span className="font-semibold text-base-content/70">
            PR number (опционально)
          </span>
          <input
            type="text"
            inputMode="numeric"
            pattern="[0-9]*"
            className={`input input-sm w-full font-mono text-xs ${
              prInvalid ? "input-bordered border-error" : "input-bordered"
            }`}
            value={form.prNumber}
            onChange={(e) => {
              onChange({ ...form, prNumber: e.target.value });
            }}
            disabled={disabled}
            placeholder="например 1234"
            aria-label="Номер PR для отслеживания"
            aria-invalid={prInvalid}
            data-testid="bind-repository-pr-number"
          />
          {prInvalid ? (
            <span className="text-error">
              PR number — целое число больше нуля.
            </span>
          ) : (
            <span className="text-base-content/50">
              Если оставить пустым — PR не отслеживается.
            </span>
          )}
        </label>
      </div>
    </div>
  );
}
