import { Check, ChevronDown } from "lucide-react";
import { useEffect, useRef, useState } from "react";

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
  const [open, setOpen] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<IntentStatus | null>(null);
  const [rejectReason, setRejectReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    setPendingStatus(null);
    setRejectReason("");
    setError(null);
  }, [intent.id, intent.status]);

  useEffect(() => {
    if (!open) return;
    const onDocClick = (event: MouseEvent) => {
      if (!containerRef.current) return;
      if (!containerRef.current.contains(event.target as Node)) {
        setOpen(false);
        setPendingStatus(null);
        setRejectReason("");
        setError(null);
      }
    };
    const onEsc = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
        setPendingStatus(null);
        setRejectReason("");
        setError(null);
      }
    };
    document.addEventListener("mousedown", onDocClick);
    document.addEventListener("keydown", onEsc);
    return () => {
      document.removeEventListener("mousedown", onDocClick);
      document.removeEventListener("keydown", onEsc);
    };
  }, [open]);

  const apply = async (next: IntentStatus, reason?: string) => {
    setBusy(true);
    setError(null);
    try {
      const updated = await httpPost<IntentDetail>(
        intentsEndpoints.setIntentStatus(intent.id),
        {
          status: next,
          reject_reason: reason
        }
      );
      onSaved(updated);
      setOpen(false);
      setPendingStatus(null);
      setRejectReason("");
    } catch (err: unknown) {
      setError(toErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const onPick = (next: IntentStatus) => {
    if (next === intent.status) {
      setOpen(false);
      return;
    }
    if (next === "reject") {
      setPendingStatus(next);
      return;
    }
    void apply(next);
  };

  const currentMeta = intentStatusMeta[intent.status];
  const showRejectInput = pendingStatus === "reject";
  const canSubmitReject = rejectReason.trim().length > 0;

  return (
    <div ref={containerRef} className="relative inline-block">
      <button
        type="button"
        onClick={() => {
          setOpen((value) => !value);
        }}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`Статус: ${currentMeta.label}. Нажмите чтобы изменить`}
        className="inline-flex h-7 items-center gap-1 rounded-full px-2.5 text-xs font-bold transition-shadow hover:shadow-sm"
        style={{ background: currentMeta.surface, color: currentMeta.ink }}
      >
        <span>{currentMeta.label}</span>
        <ChevronDown aria-hidden size={12} strokeWidth={2.5} />
      </button>

      {open ? (
        <div
          role="menu"
          className="absolute left-0 top-[calc(100%+6px)] z-20 w-72 rounded-md border border-base-300 bg-base-100 p-1 shadow-lg"
        >
          {intentStatusOrder.map((item) => {
            const meta = intentStatusMeta[item];
            const active = item === intent.status;
            return (
              <button
                key={item}
                role="menuitemradio"
                aria-checked={active}
                type="button"
                disabled={busy}
                onClick={() => {
                  onPick(item);
                }}
                className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm hover:bg-base-200 disabled:opacity-60"
              >
                <span
                  className="inline-flex h-5 items-center rounded-full px-2 text-[11px] font-bold"
                  style={{ background: meta.surface, color: meta.ink }}
                >
                  {meta.label}
                </span>
                {active ? (
                  <Check
                    aria-hidden
                    size={14}
                    strokeWidth={2.5}
                    className="ml-auto text-primary"
                  />
                ) : null}
              </button>
            );
          })}

          {showRejectInput ? (
            <div className="mt-1 border-t border-base-300 px-2 pb-2 pt-2">
              <label className="grid gap-1 text-xs text-base-content/70">
                <span className="font-semibold text-base-content">
                  Причина отклонения
                </span>
                <textarea
                  className="textarea textarea-bordered textarea-sm w-full"
                  value={rejectReason}
                  onChange={(event) => {
                    setRejectReason(event.target.value);
                  }}
                  placeholder="Почему intent отклонён"
                  rows={3}
                  disabled={busy}
                  autoFocus
                />
              </label>
              <div className="mt-2 flex justify-end gap-2">
                <Button
                  type="button"
                  onClick={() => {
                    setPendingStatus(null);
                    setRejectReason("");
                    setError(null);
                  }}
                  disabled={busy}
                >
                  Отмена
                </Button>
                <Button
                  variant="primary"
                  type="button"
                  onClick={() => {
                    if (!canSubmitReject) return;
                    void apply("reject", rejectReason.trim());
                  }}
                  disabled={!canSubmitReject || busy}
                >
                  {busy ? "Отклоняем…" : "Отклонить"}
                </Button>
              </div>
            </div>
          ) : null}

          {error ? (
            <p role="alert" className="m-0 px-2 pb-2 pt-1 text-xs text-error">
              {error}
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
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
