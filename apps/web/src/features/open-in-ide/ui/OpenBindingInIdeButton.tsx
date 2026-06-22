import { Code2 } from "lucide-react";
import { useState } from "react";

import { useDetectedIdeProviders } from "@/entities/capability";
import { errorMessage } from "@/shared/lib";
import { Button } from "@/shared/ui";

import { openBindingInIde } from "../api/open-in-ide-api";

interface OpenBindingInIdeButtonProps {
  intentId: string;
  bindingId: string;
  fullName: string;
  disabled?: boolean;
  /** Render as a row inside an overflow menu instead of a standalone button. */
  asMenuItem?: boolean;
}

/**
 * Open one repository binding's clone in the operator's selected IDE.
 * `asMenuItem` renders the menu-row variant (RepositoryBindingRow); otherwise a
 * standalone Button. Hidden when no IDE provider is detected.
 */
export function OpenBindingInIdeButton({
  intentId,
  bindingId,
  fullName,
  disabled,
  asMenuItem
}: OpenBindingInIdeButtonProps) {
  const providers = useDetectedIdeProviders();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (providers.length === 0) return null;

  const handleClick = () => {
    setBusy(true);
    setError(null);
    void (async () => {
      try {
        await openBindingInIde(intentId, bindingId);
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
          aria-label={`Открыть ${fullName} в IDE`}
          data-testid={`open-binding-in-ide-${bindingId}`}
          disabled={busy || disabled === true}
          onClick={handleClick}
          className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm text-base-content hover:bg-base-200 disabled:opacity-60"
        >
          <Code2 aria-hidden size={14} strokeWidth={2} />
          {busy ? "Открываем…" : "Открыть в IDE"}
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
        aria-label={`Открыть ${fullName} в IDE`}
        data-testid={`open-binding-in-ide-${bindingId}`}
        disabled={busy || disabled === true}
        icon={<Code2 aria-hidden size={14} strokeWidth={2} />}
        onClick={handleClick}
      >
        {busy ? "Открываем…" : "Открыть в IDE"}
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
  return errorMessage(err, {
    base: "Не удалось открыть",
    byStatus: {
      422: "Не удалось открыть: IDE-провайдер недоступен или клон не готов.",
      404: "Binding не найден."
    },
    fallback: "Не удалось открыть в IDE."
  });
}
