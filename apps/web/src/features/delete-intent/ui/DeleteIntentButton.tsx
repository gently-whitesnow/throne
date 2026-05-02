import { useState } from "react";

import { HttpError, httpDelete, intentsEndpoints } from "@/shared/api";
import { Button } from "@/shared/ui";

interface DeleteIntentButtonProps {
  intentId: string;
  onDeleted: () => void;
}

export function DeleteIntentButton({
  intentId,
  onDeleted
}: DeleteIntentButtonProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleDelete = async () => {
    if (busy) return;
    if (!window.confirm("Удалить Intent и всю историю версий?")) return;
    setBusy(true);
    setError(null);
    try {
      await httpDelete(intentsEndpoints.deleteIntent(intentId));
      onDeleted();
    } catch (err: unknown) {
      const message =
        err instanceof HttpError
          ? `Не удалось удалить (${String(err.status)}).`
          : "Не удалось удалить.";
      setError(message);
      setBusy(false);
    }
  };

  return (
    <>
      <Button
        onClick={() => {
          void handleDelete();
        }}
        disabled={busy}
      >
        {busy ? "Удаляем…" : "Удалить"}
      </Button>
      {error ? (
        <p role="alert" className="m-0 text-sm text-error">
          {error}
        </p>
      ) : null}
    </>
  );
}
