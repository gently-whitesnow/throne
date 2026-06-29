import { Trash2 } from "lucide-react";
import { useState } from "react";

import {
  useDeletePromptPart,
  usePromptPart,
  useReplacePromptPartText,
  type PromptPartListItem
} from "@/entities/prompt-part";
import { computeMinimalTextDelta } from "@/shared/lib";
import { Button } from "@/shared/ui";

import {
  formatDeleteError,
  formatReplaceError
} from "./PromptPartDialogErrors";

export function EditPromptPartBody({
  part,
  onClose
}: {
  part: PromptPartListItem;
  onClose: () => void;
}) {
  const detail = usePromptPart(part.id);
  const replace = useReplacePromptPartText();
  const remove = useDeletePromptPart();
  const [draft, setDraft] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  const loadedText = detail.data?.text ?? "";
  const value = draft ?? loadedText;

  const submitText = () => {
    if (!detail.data) return;
    setError(null);
    const delta = computeMinimalTextDelta(detail.data.text, value);
    if (delta === null) {
      onClose();
      return;
    }
    replace.mutate(
      {
        id: part.id,
        expectedVersion: detail.data.current_version,
        oldText: delta.oldText,
        newText: delta.newText
      },
      {
        onSuccess: onClose,
        onError: (err) => {
          setError(formatReplaceError(err));
        }
      }
    );
  };

  const submitDelete = () => {
    setError(null);
    remove.mutate(part.id, {
      onSuccess: onClose,
      onError: (err) => {
        setError(formatDeleteError(err));
      }
    });
  };

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-1">
          <span className="text-[13px] font-semibold text-base-content">
            Key
          </span>
          <p className="m-0 rounded-md border border-base-300 bg-base-200 px-3 py-2 font-mono text-[13px]">
            {part.key}
          </p>
        </div>
        <div className="flex flex-col gap-1">
          <span className="text-[13px] font-semibold text-base-content">
            Описание
          </span>
          <p className="m-0 rounded-md border border-base-300 bg-base-200 px-3 py-2 text-[13px] text-base-content/70">
            {part.description ?? "—"}
          </p>
        </div>
      </div>
      <p className="m-0 text-xs text-base-content/60">
        Описание задаётся при создании; обновление описания пока не поддержано
        API.
      </p>

      <label className="flex flex-col gap-1">
        <span className="text-[13px] font-semibold text-base-content">
          Текст
        </span>
        {detail.isPending ? (
          <p className="m-0 text-[13px] text-base-content/60">
            Загрузка текста…
          </p>
        ) : (
          <textarea
            className="textarea textarea-bordered min-h-60 w-full font-mono text-[13px] leading-relaxed"
            value={value}
            onChange={(e) => {
              setDraft(e.target.value);
            }}
            rows={14}
            aria-label="Текст блока"
          />
        )}
      </label>

      {error ? (
        <p role="alert" className="m-0 text-sm text-error">
          {error}
        </p>
      ) : null}

      <div className="flex items-center justify-between gap-2">
        {confirmingDelete ? (
          <div className="flex items-center gap-2">
            <span className="text-[13px] text-base-content/70">
              Удалить блок?
            </span>
            <Button
              type="button"
              className="btn-error"
              onClick={submitDelete}
              disabled={remove.isPending}
            >
              {remove.isPending ? "Удаляем…" : "Да, удалить"}
            </Button>
            <Button
              type="button"
              onClick={() => {
                setConfirmingDelete(false);
              }}
              disabled={remove.isPending}
            >
              Отмена
            </Button>
          </div>
        ) : (
          <Button
            type="button"
            icon={<Trash2 aria-hidden size={14} strokeWidth={2} />}
            className="text-error"
            onClick={() => {
              setError(null);
              setConfirmingDelete(true);
            }}
          >
            Удалить
          </Button>
        )}

        <div className="flex gap-2">
          <Button type="button" onClick={onClose} disabled={replace.isPending}>
            Закрыть
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={submitText}
            disabled={replace.isPending || detail.isPending}
          >
            {replace.isPending ? "Сохраняем…" : "Сохранить текст"}
          </Button>
        </div>
      </div>
    </div>
  );
}
