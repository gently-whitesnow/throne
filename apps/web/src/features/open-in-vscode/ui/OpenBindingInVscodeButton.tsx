import { ExternalLink } from "lucide-react";
import { useState } from "react";

import { useCapabilityEnabled } from "@/entities/capability";
import { HttpError } from "@/shared/api";
import { Button } from "@/shared/ui";

import { openBindingInVscode } from "../api/open-in-vscode-api";

interface OpenBindingInVscodeButtonProps {
  intentId: string;
  bindingId: string;
  fullName: string;
  disabled?: boolean;
  /** Render as a row inside an overflow menu instead of a standalone button. */
  asMenuItem?: boolean;
}

/**
 * «Open in VS Code» для конкретного repository-binding'а. Видна только когда
 * capability `vscode` детектится И включена. `asMenuItem` отдаёт строку для
 * overflow-меню (см. RepositoryBindingRow), иначе — самостоятельную кнопку.
 * `disabled` дополнительно гасит действие (например, пока клон не готов).
 */
export function OpenBindingInVscodeButton({
  intentId,
  bindingId,
  fullName,
  disabled,
  asMenuItem
}: OpenBindingInVscodeButtonProps) {
  const enabled = useCapabilityEnabled("vscode");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!enabled) return null;

  const handleClick = () => {
    setBusy(true);
    setError(null);
    void (async () => {
      try {
        await openBindingInVscode(intentId, bindingId);
      } catch (err) {
        setError(formatError(err));
      } finally {
        setBusy(false);
      }
    })();
  };

  if (asMenuItem) {
    return (
      <>
        <button
          type="button"
          role="menuitem"
          aria-label={`Open ${fullName} в VS Code`}
          data-testid={`open-binding-in-vscode-${bindingId}`}
          disabled={busy || disabled === true}
          onClick={handleClick}
          className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm text-base-content hover:bg-base-200 disabled:opacity-60"
        >
          <ExternalLink aria-hidden size={14} strokeWidth={2} />
          {busy ? "Открываем…" : "Открыть в VS Code"}
        </button>
        {error !== null ? (
          <span role="alert" className="block px-2 pb-1 text-xs text-error">
            {error}
          </span>
        ) : null}
      </>
    );
  }

  return (
    <div className="flex flex-col items-end gap-1">
      <Button
        aria-label={`Open ${fullName} в VS Code`}
        data-testid={`open-binding-in-vscode-${bindingId}`}
        disabled={busy || disabled === true}
        icon={<ExternalLink aria-hidden size={14} strokeWidth={2} />}
        onClick={handleClick}
      >
        {busy ? "Открываем…" : "VS Code"}
      </Button>
      {error !== null ? (
        <span role="alert" className="text-xs text-error">
          {error}
        </span>
      ) : null}
    </div>
  );
}

function formatError(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 422) {
      return "Не удалось открыть: код-CLI недоступен, capability выключена или клон не готов.";
    }
    if (err.status === 404) {
      return "Binding не найден.";
    }
    return `Не удалось открыть (${String(err.status)}).`;
  }
  return "Не удалось открыть в VS Code.";
}
