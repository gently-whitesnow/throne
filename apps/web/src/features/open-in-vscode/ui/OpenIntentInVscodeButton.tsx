import { ExternalLink } from "lucide-react";
import { useState } from "react";

import { useCapabilityEnabled } from "@/entities/capability";
import { errorMessage } from "@/shared/lib";
import { Button } from "@/shared/ui";

import { openIntentInVscode } from "../api/open-in-vscode-api";

interface OpenIntentInVscodeButtonProps {
  intentId: string;
}

/**
 * Кнопка «Open in VS Code» для всего workspace интента (single-root window с
 * подпапками-репами — Slice 2 Q4). Видна только когда capability `vscode`
 * детектится И включена. Без inline-hint'ов: если capability выключена,
 * кнопка просто отсутствует, описание и тогл живут на странице `/settings`.
 */
export function OpenIntentInVscodeButton({
  intentId
}: OpenIntentInVscodeButtonProps) {
  const enabled = useCapabilityEnabled("vscode");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!enabled) return null;

  const handleClick = () => {
    setBusy(true);
    setError(null);
    void (async () => {
      try {
        await openIntentInVscode(intentId);
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
        aria-label="Open workspace интента в VS Code"
        data-testid="open-intent-in-vscode"
        disabled={busy}
        icon={<ExternalLink aria-hidden size={14} strokeWidth={2} />}
        onClick={handleClick}
      >
        {busy ? "Открываем…" : "Open in VS Code"}
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
      422: "Не удалось открыть: code CLI недоступен или capability выключена.",
      404: "Интент не найден."
    },
    fallback: "Не удалось открыть в VS Code."
  });
}
