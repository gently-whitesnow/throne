import { ImagePlus, Plus, X } from "lucide-react";
import { useEffect, useEffectEvent, useId, useState } from "react";
import { createPortal } from "react-dom";

import type { IntentAttachment, IntentDetail } from "@/entities/intent";
import { useTagPicker } from "@/entities/tag";
import {
  HttpError,
  INTENT_ATTACHMENTS_CHANGED_EVENT,
  httpPost,
  httpPostForm,
  intentsEndpoints
} from "@/shared/api";
import { filesFromClipboard } from "@/shared/lib";
import { Button, TagMultiSelect } from "@/shared/ui";

const MAX_ATTACHMENTS = 10;
const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;

interface CreateIntentButtonProps {
  onCreated?: (intent: IntentDetail) => void;
}

export function CreateIntentButton({ onCreated }: CreateIntentButtonProps) {
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [tags, setTags] = useState<string[]>([]);
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const tagPicker = useTagPicker();
  const titleId = useId();
  const descriptionId = useId();

  const reset = () => {
    setText("");
    setTags([]);
    setFiles([]);
    setError(null);
  };

  const close = useEffectEvent(() => {
    if (busy) return;
    reset();
    setOpen(false);
  });

  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        close();
      }
    };

    const { overflow } = document.body.style;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", handleKeyDown);

    return () => {
      document.body.style.overflow = overflow;
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [close, open]);

  const addFiles = (nextFiles: Iterable<File> | null) => {
    if (!nextFiles) return;

    const accepted = [...files];
    const problems: string[] = [];
    for (const file of nextFiles) {
      if (accepted.length >= MAX_ATTACHMENTS) {
        problems.push(
          `Можно приложить максимум ${String(MAX_ATTACHMENTS)} файлов.`
        );
        break;
      }
      if (!file.type.startsWith("image/")) {
        problems.push(`${file.name}: сейчас принимаем только изображения.`);
        continue;
      }
      if (file.size > MAX_ATTACHMENT_BYTES) {
        problems.push(`${file.name}: файл больше 10 МБ.`);
        continue;
      }
      accepted.push(file);
    }

    setFiles(accepted);
    setError(problems.length > 0 ? unique(problems).join(" ") : null);
  };

  const pasteImages = (clipboard: DataTransfer) => {
    const pastedFiles = filesFromClipboard(clipboard);
    if (pastedFiles.length === 0) return;

    addFiles(pastedFiles);
  };

  const removeFile = (index: number) => {
    setFiles((current) => current.filter((_, i) => i !== index));
  };

  const submit = async () => {
    if (busy || text.trim().length === 0) return;
    setBusy(true);
    setError(null);
    try {
      const filesToUpload = files;
      const created = await httpPost<IntentDetail>(
        intentsEndpoints.createIntent(),
        {
          text,
          tag_names: tags.length > 0 ? tags : undefined
        }
      );
      reset();
      setOpen(false);
      onCreated?.(created);
      if (filesToUpload.length > 0) {
        void uploadAttachments(created.id, filesToUpload);
      }
    } catch (err: unknown) {
      const message =
        err instanceof HttpError
          ? `Не удалось создать (${String(err.status)}).`
          : "Не удалось создать.";
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  if (!open) {
    return (
      <Button
        aria-label="Создать intent"
        icon={<Plus aria-hidden size={18} strokeWidth={2.4} />}
        variant="primary"
        onClick={() => {
          setOpen(true);
        }}
      >
        Создать
      </Button>
    );
  }

  return createPortal(
    <div
      className="modal modal-open modal-bottom sm:modal-middle"
      role="presentation"
      onClick={() => {
        close();
      }}
    >
      <div
        className="modal-box max-h-[min(820px,calc(100vh-48px))] w-full max-w-2xl border border-base-300 bg-base-100"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
        onClick={(event) => {
          event.stopPropagation();
        }}
      >
        <div className="mb-5 flex items-start justify-between gap-4">
          <div className="flex flex-col gap-2">
            <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
              Новый intent
            </p>
            <h3
              id={titleId}
              className="m-0 text-xl font-semibold leading-tight"
            >
              Сформулируйте задачу в одном окне
            </h3>
            <p
              id={descriptionId}
              className="m-0 max-w-[46ch] text-sm leading-relaxed text-base-content/70"
            >
              Добавьте текст, теги и изображения. Клик вне окна закроет форму.
            </p>
          </div>
          <button
            type="button"
            className="btn btn-sm btn-circle btn-ghost"
            onClick={() => {
              close();
            }}
            aria-label="Закрыть форму создания intent"
            disabled={busy}
          >
            <X aria-hidden size={16} strokeWidth={2} />
          </button>
        </div>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void submit();
          }}
          className="flex flex-col gap-3.5"
        >
          <textarea
            className="textarea textarea-bordered min-h-[220px] w-full text-base"
            placeholder="Кратко опишите intent"
            value={text}
            onChange={(e) => {
              setText(e.target.value);
            }}
            onPaste={(e) => {
              pasteImages(e.clipboardData);
            }}
            rows={8}
            aria-label="Текст intent"
            autoFocus
          />
          <TagMultiSelect
            value={tags}
            onChange={setTags}
            availableTags={tagPicker.availableTags}
            onRequestCreate={tagPicker.createTag}
            loadError={tagPicker.loadError}
            disabled={busy}
            placeholder="Добавить тег…"
            ariaLabel="Теги intent"
          />
          <label className="flex cursor-pointer flex-col gap-1.5 rounded-md border border-dashed border-base-300 bg-base-200 px-4 py-3 transition-colors hover:border-primary hover:bg-primary/5">
            <input
              type="file"
              accept="image/*"
              multiple
              className="sr-only"
              onChange={(e) => {
                addFiles(e.currentTarget.files);
                e.currentTarget.value = "";
              }}
            />
            <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-primary">
              <ImagePlus aria-hidden size={16} strokeWidth={2} />
              Приложить изображения
            </span>
            <span className="text-xs leading-relaxed text-base-content/60">
              До 10 файлов, каждый до 10 МБ. Можно вставить картинку из буфера.
            </span>
          </label>
          {files.length > 0 ? (
            <ul
              className="m-0 flex max-h-44 list-none flex-col gap-1.5 overflow-y-auto p-0"
              aria-label="Выбранные файлы"
            >
              {files.map((file, index) => (
                <li
                  key={`${file.name}-${String(file.lastModified)}-${String(index)}`}
                  className="grid grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-2 rounded-md border border-base-300 bg-base-100 px-3 py-2 text-[13px]"
                >
                  <span className="min-w-0 truncate">{file.name}</span>
                  <span className="tabular-nums text-base-content/60">
                    {formatBytes(file.size)}
                  </span>
                  <button
                    type="button"
                    className="btn btn-xs btn-ghost btn-circle"
                    onClick={() => {
                      removeFile(index);
                    }}
                    aria-label={`Убрать ${file.name}`}
                    disabled={busy}
                  >
                    <X aria-hidden size={14} strokeWidth={2} />
                  </button>
                </li>
              ))}
            </ul>
          ) : null}
          {error ? (
            <p role="alert" className="m-0 text-sm text-error">
              {error}
            </p>
          ) : null}
          <div className="flex justify-end gap-2 max-sm:flex-col-reverse [&>*]:max-sm:w-full">
            <Button
              type="button"
              onClick={() => {
                close();
              }}
              disabled={busy}
            >
              Отмена
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={busy || text.trim().length === 0}
            >
              {busy ? "Создаём…" : "Создать intent"}
            </Button>
          </div>
        </form>
      </div>
    </div>,
    document.body
  );
}

async function uploadAttachments(intentId: string, files: File[]) {
  for (const file of files) {
    const form = new FormData();
    form.append("file", file, file.name);
    try {
      await httpPostForm<IntentAttachment>(
        intentsEndpoints.uploadIntentAttachment(intentId),
        form
      );
      window.dispatchEvent(
        new CustomEvent(INTENT_ATTACHMENTS_CHANGED_EVENT, {
          detail: { intentId }
        })
      );
    } catch (err) {
      const message =
        err instanceof HttpError
          ? `Не удалось загрузить ${file.name} (${String(err.status)}).`
          : `Не удалось загрузить ${file.name}.`;
      window.dispatchEvent(
        new CustomEvent(INTENT_ATTACHMENTS_CHANGED_EVENT, {
          detail: { intentId, error: message }
        })
      );
    }
  }
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${String(bytes)} Б`;
  const mb = bytes / (1024 * 1024);
  if (mb >= 1) return `${mb.toFixed(mb >= 10 ? 0 : 1)} МБ`;
  return `${(bytes / 1024).toFixed(0)} КБ`;
}

function unique(values: string[]): string[] {
  return [...new Set(values)];
}
