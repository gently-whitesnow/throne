import { Link2, X } from "lucide-react";

import type { PickerSelection } from "../model/picker-types";
import { manualHostError } from "../model/repo-key";

interface SimpleRepositoryChipProps {
  selection: PickerSelection;
  gitlabHost: string | null;
  disabled: boolean;
  onRemove: () => void;
}

/**
 * Default per-selection chip used by потребителями picker'а без дополнительной
 * per-chip метаданных (тег-страница). Показывает имя репозитория, иконку
 * источника (search / manual SSH) и host-validation для manual записей.
 */
export function SimpleRepositoryChip({
  selection,
  gitlabHost,
  disabled,
  onRemove
}: SimpleRepositoryChipProps) {
  const { ref, source } = selection;
  const isManual = source === "manual";
  const hostError = isManual ? manualHostError(ref, gitlabHost) : null;

  return (
    <div
      className="flex flex-col gap-1 rounded-md border border-base-300 bg-base-100 p-3"
      data-testid={`repository-picker-chip-${ref.full_name}`}
    >
      <div className="flex items-center justify-between gap-2">
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
        <button
          type="button"
          className="btn btn-ghost btn-xs btn-circle"
          onClick={onRemove}
          disabled={disabled}
          aria-label={`Убрать ${ref.full_name}`}
          data-testid={`repository-picker-chip-remove-${ref.full_name}`}
        >
          <X aria-hidden size={14} strokeWidth={2} />
        </button>
      </div>
      {hostError !== null ? (
        <span
          className="text-xs text-error"
          data-testid={`repository-picker-chip-host-error-${ref.full_name}`}
        >
          {hostError}
        </span>
      ) : null}
    </div>
  );
}
