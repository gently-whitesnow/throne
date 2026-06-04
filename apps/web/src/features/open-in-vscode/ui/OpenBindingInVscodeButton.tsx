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
}

/**
 * Кнопка «Open in VS Code» рядом с конкретным repository-binding'ом. Видна
 * только когда capability `vscode` детектится И включена. Дополнительно
 * можно отключить через `disabled` (например, пока клон не готов).
 */
export function OpenBindingInVscodeButton({
  intentId,
  bindingId,
  fullName,
  disabled
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
