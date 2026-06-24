import { Plus } from "lucide-react";
import { useId, useState } from "react";

import { parseSshUrl } from "../model/parse-ssh-url";
import type { GitRepositoryRef } from "../model/types";

interface ManualSshUrlInputProps {
  disabled: boolean;
  /** Add the parsed coordinate; returns an error string to surface inline. */
  onAdd: (ref: GitRepositoryRef) => string | null;
}

/**
 * Paste-an-SSH-URL escape hatch for repos the autocomplete cannot find (no
 * membership, but git access exists). Host/provider mismatches are reported on
 * the resulting chip, not here — this only catches malformed URLs and dupes.
 */
export function ManualSshUrlInput({ disabled, onAdd }: ManualSshUrlInputProps) {
  const [value, setValue] = useState("");
  const [error, setError] = useState<string | null>(null);
  const inputId = useId();

  function add() {
    const parsed = parseSshUrl(value);
    if (parsed.kind === "error") {
      setError(parsed.message);
      return;
    }
    const addError = onAdd(parsed.ref);
    if (addError !== null) {
      setError(addError);
      return;
    }
    setValue("");
    setError(null);
  }

  return (
    <div className="flex flex-col gap-1">
      <label
        htmlFor={inputId}
        className="text-xs font-semibold text-base-content/70"
      >
        Или вставьте SSH-URL
      </label>
      <div className="flex items-center gap-2">
        <input
          id={inputId}
          type="text"
          className="input input-bordered input-sm grow font-mono text-xs"
          placeholder="git@github.com:owner/repo.git"
          value={value}
          disabled={disabled}
          aria-invalid={error !== null}
          data-testid="repository-picker-ssh-url"
          onChange={(e) => {
            setValue(e.target.value);
            if (error !== null) setError(null);
          }}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              add();
            }
          }}
        />
        <button
          type="button"
          className="btn btn-sm"
          onClick={add}
          disabled={disabled || value.trim().length === 0}
          data-testid="repository-picker-ssh-add"
        >
          <Plus aria-hidden size={14} strokeWidth={2} />
          Добавить
        </button>
      </div>
      {error !== null ? (
        <span
          role="alert"
          className="text-xs text-error"
          data-testid="repository-picker-ssh-error"
        >
          {error}
        </span>
      ) : null}
    </div>
  );
}
