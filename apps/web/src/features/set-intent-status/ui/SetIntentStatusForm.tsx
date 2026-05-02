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
    <section
      className="card mb-4 grid gap-3.5 rounded-lg border border-base-300 bg-base-100 px-5 py-4 shadow-sm"
      aria-label="Статус intent"
    >
      <div className="flex flex-wrap items-center gap-2.5">
        <span
          className="inline-flex h-7 items-center rounded-full px-3 text-xs font-bold"
          style={{ background: currentMeta.surface, color: currentMeta.ink }}
        >
          {currentMeta.label}
        </span>
        <p className="m-0 text-sm leading-snug text-base-content/70">
          Статус влияет на фильтрацию и на то, что агент видит в текущем intent.
        </p>
      </div>

      <div className="grid gap-3">
        <label className="grid gap-1.5 text-sm text-base-content/70">
          <span className="font-semibold text-base-content">Новый статус</span>
          <select
            className="select select-sm select-bordered w-full max-w-sm"
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
          <label className="grid gap-1.5 text-sm text-base-content/70">
            <span className="font-semibold text-base-content">
              Причина отклонения
            </span>
            <textarea
              className="textarea textarea-bordered w-full"
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

        <div className="flex">
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
          <p role="alert" className="m-0 text-sm text-error">
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
