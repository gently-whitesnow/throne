import { AlertTriangle } from "lucide-react";
import { useId, useState } from "react";

import { deleteTag, useTagUsage, type Tag } from "@/entities/tag";
import { errorMessage } from "@/shared/lib";
import { Modal } from "@/shared/ui";

interface DeleteTagDialogProps {
  tag: Tag;
  onClose: () => void;
  onDeleted: (tagId: string) => void;
}

/**
 * Подтверждение удаления тега в стиле приложения вместо `window.confirm`. Сразу
 * показывает, к скольким интентам привязан тег (тот же usage-кеш, что и в
 * списке), и при необходимости предлагает «открепить и удалить».
 */
export function DeleteTagDialog({ tag, onClose, onDeleted }: DeleteTagDialogProps) {
  const titleId = useId();
  const descriptionId = useId();
  const usageQuery = useTagUsage(tag.id);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const intentsCount = usageQuery.data?.intents_count ?? 0;
  const detach = intentsCount > 0;
  const loadingUsage = usageQuery.isPending;

  const handleConfirm = () => {
    setBusy(true);
    setError(null);
    void (async () => {
      try {
        await deleteTag(tag.id, detach);
        onDeleted(tag.id);
        onClose();
      } catch (err: unknown) {
        setError(errorMessage(err, { base: "Не удалось удалить тег" }));
        setBusy(false);
      }
    })();
  };

  return (
    <Modal
      onClose={busy ? () => undefined : onClose}
      labelledBy={titleId}
      describedBy={descriptionId}
      boxClassName="w-full max-w-sm"
    >
      <div className="flex items-start gap-3">
        <span aria-hidden className="mt-0.5 shrink-0 text-error">
          <AlertTriangle size={20} strokeWidth={2} />
        </span>
        <div className="flex min-w-0 flex-col gap-2">
          <h3 id={titleId} className="m-0 text-base font-semibold leading-tight">
            Удалить #{tag.name}?
          </h3>
          <p
            id={descriptionId}
            className="m-0 text-sm leading-relaxed text-base-content/70"
          >
            {loadingUsage
              ? "Проверяем привязки…"
              : detach
                ? `Тег привязан к ${String(intentsCount)} ${intentsCount === 1 ? "интенту" : "интентам"}. Он будет откреплён от них и удалён без возможности восстановления.`
                : "Тег нигде не используется. Действие необратимо."}
          </p>
          {error !== null && (
            <p role="alert" className="m-0 text-sm text-error">
              {error}
            </p>
          )}
        </div>
      </div>
      <div className="mt-5 flex justify-end gap-2">
        <button
          type="button"
          className="btn btn-sm btn-ghost"
          onClick={onClose}
          disabled={busy}
        >
          Отмена
        </button>
        <button
          type="button"
          className="btn btn-sm btn-error"
          onClick={handleConfirm}
          disabled={busy || loadingUsage}
        >
          {busy
            ? "Удаляем…"
            : detach
              ? "Открепить и удалить"
              : "Удалить"}
        </button>
      </div>
    </Modal>
  );
}
