import { KeyRound } from "lucide-react";
import { useState } from "react";

import type { McpTokenIssued } from "@/entities/mcp-token";
import { issueMcpToken } from "@/entities/mcp-token";
import { HttpError } from "@/shared/api";
import { Button } from "@/shared/ui";

interface GenerateMcpTokenButtonProps {
  onIssued: (issued: McpTokenIssued) => void;
}

export function GenerateMcpTokenButton({
  onIssued
}: GenerateMcpTokenButtonProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleClick = async () => {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const issued = await issueMcpToken();
      onIssued(issued);
    } catch (err: unknown) {
      const message =
        err instanceof HttpError
          ? `Не удалось сгенерировать токен (${String(err.status)}).`
          : "Не удалось сгенерировать токен.";
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col gap-2">
      <Button
        variant="primary"
        icon={<KeyRound aria-hidden size={16} strokeWidth={2} />}
        disabled={busy}
        onClick={() => {
          void handleClick();
        }}
      >
        {busy ? "Генерируем…" : "Сгенерировать токен"}
      </Button>
      {error ? (
        <p role="alert" className="m-0 text-sm text-error">
          {error}
        </p>
      ) : null}
    </div>
  );
}
