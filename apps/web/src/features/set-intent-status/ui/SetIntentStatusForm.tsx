import { useEffect, useState } from "react";

import {
  intentStatusMeta,
  intentStatusOrder,
  type IntentDetail,
  type IntentStatus
} from "@/entities/intent";
import { HttpError, httpPost, intentsEndpoints } from "@/shared/api";
import { Button } from "@/shared/ui";

interface SetIntentStatusFormProps {
  intent: IntentDetail;
  onSaved: (next: IntentDetail) => void;
}

export function SetIntentStatusForm({
  intent,
  onSaved
}: SetIntentStatusFormProps) {
  const [status, setStatus] = useState<IntentStatus>(intent.status);
  const [rejectReason, setRejectReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setStatus(intent.status);
    setRejectReason("");
    setError(null);
  }, [intent.id, intent.status]);

  const statusChanged = status !== intent.status;
  const needsRejectReason = status === "reject";
  const canSave =
    statusChanged && (!needsRejectReason || rejectReason.trim().length > 0);

  const onSubmit = async () => {
    if (!canSave) return;
    setBusy(true);
    setError(null);
    try {
      const next = await httpPost<IntentDetail>(
        intentsEndpoints.setIntentStatus(intent.id),
        {
          status,
          reject_reason: needsRejectReason ? rejectReason.trim() : undefined
        }
      );
      onSaved(next);
    } catch (err: unknown) {
      setError(toErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const currentMeta = intentStatusMeta[intent.status];

  return (
    <section className="intent-status-panel" aria-label="Статус intent">
      <div className="intent-status-panel__summary">
        <span
          className="intent-status-panel__badge"
          style={{ background: currentMeta.surface, color: currentMeta.ink }}
        >
          {currentMeta.label}
        </span>
        <p className="intent-status-panel__hint">
          Статус влияет на фильтрацию и на то, что агент видит в текущем intent.
        </p>
      </div>

      <div className="intent-status-panel__controls">
        <label className="intent-status-panel__field">
          <span>Новый статус</span>
          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value as IntentStatus);
              setError(null);
            }}
            disabled={busy}
          >
            {intentStatusOrder.map((item) => (
              <option key={item} value={item}>
                {intentStatusMeta[item].label}
              </option>
            ))}
          </select>
        </label>

        {needsRejectReason && (
          <label className="intent-status-panel__field">
            <span>Причина отклонения</span>
            <textarea
              value={rejectReason}
              onChange={(e) => {
                setRejectReason(e.target.value);
                setError(null);
              }}
              placeholder="Почему intent отклонен"
              rows={4}
              disabled={busy}
            />
          </label>
        )}

        <div className="intent-status-panel__actions">
          <Button
            variant="primary"
            onClick={() => {
              void onSubmit();
            }}
            disabled={!canSave || busy}
          >
            {busy ? "Сохраняем…" : "Обновить статус"}
          </Button>
        </div>

        {error ? (
          <p role="alert" className="intent-status-panel__error">
            {error}
          </p>
        ) : null}
      </div>
    </section>
  );
}

function toErrorMessage(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 409) {
      return "Intent уже изменился. Перезагрузите карточку и попробуйте снова.";
    }

    if (err.status === 422) {
      return "Проверьте статус и причину отклонения.";
    }

    return `Не удалось обновить статус (${String(err.status)}).`;
  }

  return "Не удалось обновить статус.";
}
