import { AlertTriangle } from "lucide-react";
import { useId, useState } from "react";

import { deleteTag, type Tag } from "@/entities/tag";
import { errorMessage } from "@/shared/lib";
import { Modal } from "@/shared/ui";

interface DeleteTagDialogProps {
  tag: Tag;
  onClose: () => void;
  onDeleted: (tagId: string) => void;
}

export function DeleteTagDialog({
  tag,
  onClose,
  onDeleted
}: DeleteTagDialogProps) {
  const titleId = useId();
  const descriptionId = useId();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleConfirm = () => {
    setBusy(true);
    setError(null);
    void (async () => {
      try {
        await deleteTag(tag.id, true);
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
          <h3
            id={titleId}
            className="m-0 text-base font-semibold leading-tight"
          >
            Удалить #{tag.name}?
          </h3>
          <p
            id={descriptionId}
            className="m-0 text-sm leading-relaxed text-base-content/70"
          >
            Тег будет удалён, все привязки к интентам сняты. Действие
            необратимо.
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
          disabled={busy}
        >
          {busy ? "Удаляем…" : "Удалить"}
        </button>
      </div>
    </Modal>
  );
}
