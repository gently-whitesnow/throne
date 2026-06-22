import { Code2 } from "lucide-react";
import { useState } from "react";

import { useDetectedIdeProviders } from "@/entities/capability";
import { errorMessage } from "@/shared/lib";
import { Button } from "@/shared/ui";

import { openIntentInIde } from "../api/open-in-ide-api";

interface OpenIntentInIdeButtonProps {
  intentId: string;
}

/**
 * Open the intent's aggregate workspace in the operator's selected IDE
 * (provider resolved server-side from `open_in_ide.selected_provider`,
 * falling back to the single detected provider). Rendered only when at
 * least one IDE provider is detected — otherwise we have nothing to open.
 */
export function OpenIntentInIdeButton({
  intentId
}: OpenIntentInIdeButtonProps) {
  const providers = useDetectedIdeProviders();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (providers.length === 0) return null;

  const handleClick = () => {
    setBusy(true);
    setError(null);
    void (async () => {
      try {
        await openIntentInIde(intentId);
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
        aria-label="Открыть workspace интента в IDE"
        data-testid="open-intent-in-ide"
        disabled={busy}
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
      422: "Не удалось открыть: ни один IDE-провайдер не доступен.",
      404: "Интент не найден."
    },
    fallback: "Не удалось открыть в IDE."
  });
}
