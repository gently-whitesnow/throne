import { useState } from "react";

import type { IntentDetail } from "@/entities/intent";

import { setIntentCleanupOnDone } from "../api/set-intent-cleanup-on-done";

interface CleanupOnDoneToggleProps {
  intent: IntentDetail;
  onSaved: (next: IntentDetail) => void;
}

export function CleanupOnDoneToggle({
  intent,
  onSaved
}: CleanupOnDoneToggleProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const toggle = (next: boolean) => {
    const prev = intent;
    setBusy(true);
    setError(null);
    onSaved({ ...intent, cleanup_local_state_on_done: next });
    void (async () => {
      try {
        const updated = await setIntentCleanupOnDone(intent.id, next);
        onSaved(updated);
      } catch {
        onSaved(prev);
        setError("Не удалось сохранить");
      } finally {
        setBusy(false);
      }
    })();
  };

  return (
    <label
      className="flex items-center gap-1 text-[11px] text-base-content/70"
      title="При завершении интента в done снести рабочую папку и остановить терминал агента"
    >
      <input
        type="checkbox"
        checked={intent.cleanup_local_state_on_done}
        disabled={busy}
        onChange={(event) => {
          toggle(event.target.checked);
        }}
        className="h-3.5 w-3.5 accent-primary"
      />
      Очистить состояние
      {error !== null ? (
        <span role="alert" className="text-error">
          {error}
        </span>
      ) : null}
    </label>
  );
}
