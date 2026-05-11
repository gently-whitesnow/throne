import { useState } from "react";

import { Button } from "@/shared/ui";

const MIN_COMMENT_LENGTH = 10;

export interface RejectPatchModalProps {
  open: boolean;
  onCancel: () => void;
  onConfirm: (comment: string) => Promise<void> | void;
  busy?: boolean;
}

/**
 * Reject confirmation with a mandatory comment. The server enforces ≥10 chars
 * after trimming; this modal mirrors the same rule client-side so the user
 * cannot submit short comments and bounce off a 422.
 */
export function RejectPatchModal({
  open,
  onCancel,
  onConfirm,
  busy
}: RejectPatchModalProps) {
  const [comment, setComment] = useState("");
  const trimmed = comment.trim();
  const tooShort = trimmed.length < MIN_COMMENT_LENGTH;

  if (!open) return null;

  return (
    <dialog className="modal modal-open" aria-modal="true">
      <div className="modal-box max-w-lg">
        <h3 className="m-0 text-lg font-bold">Отклонить патч</h3>
        <p className="mt-2 text-sm text-base-content/70">
          Комментарий обязателен — минимум {String(MIN_COMMENT_LENGTH)} символов
          после trim. Он сохраняется как часть состояния патча и учитывается
          следующим раундом анализа, чтобы тот же патч не предлагался повторно.
        </p>
        <textarea
          aria-label="Reject comment"
          className="textarea textarea-bordered mt-3 w-full"
          rows={4}
          value={comment}
          onChange={(e) => {
            setComment(e.target.value);
          }}
          placeholder="Например: «правило слишком общее, дублирует уже применённое в work»"
        />
        <p
          className={`mt-1 text-xs ${tooShort ? "text-error" : "text-base-content/60"}`}
          aria-live="polite"
        >
          Длина после trim: {String(trimmed.length)} /{" "}
          {String(MIN_COMMENT_LENGTH)}
        </p>
        <div className="modal-action">
          <Button onClick={onCancel} disabled={busy}>
            Отмена
          </Button>
          <Button
            variant="primary"
            onClick={() => void onConfirm(trimmed)}
            disabled={tooShort || busy}
          >
            {busy ? "Отклоняем..." : "Отклонить"}
          </Button>
        </div>
      </div>
    </dialog>
  );
}
