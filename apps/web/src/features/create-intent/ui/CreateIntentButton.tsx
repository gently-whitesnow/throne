import { ImagePlus, Plus, X } from "lucide-react";
import { useEffect, useEffectEvent, useId, useState } from "react";
import { createPortal } from "react-dom";

import type { IntentAttachment, IntentDetail } from "@/entities/intent";
import {
  HttpError,
  INTENT_ATTACHMENTS_CHANGED_EVENT,
  httpPost,
  httpPostForm,
  intentsEndpoints
} from "@/shared/api";
import { Button } from "@/shared/ui";

const MAX_ATTACHMENTS = 10;
const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;

interface CreateIntentButtonProps {
  onCreated?: (intent: IntentDetail) => void;
}

export function CreateIntentButton({ onCreated }: CreateIntentButtonProps) {
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [tags, setTags] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const titleId = useId();
  const descriptionId = useId();

  const reset = () => {
    setText("");
    setTags("");
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
      const tagList = tags
        .split(",")
        .map((t) => t.trim())
        .filter(Boolean);
      const created = await httpPost<IntentDetail>(
        intentsEndpoints.createIntent(),
        {
          text,
          tag_names: tagList.length > 0 ? tagList : undefined
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
      className="create-intent-modal"
      onClick={() => {
        close();
      }}
    >
      <div
        className="create-intent-modal__dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
        onClick={(event) => {
          event.stopPropagation();
        }}
      >
        <div className="create-intent-modal__header">
          <div className="create-intent-modal__title-block">
            <p className="create-intent-modal__eyebrow">Новый intent</p>
            <h3 id={titleId} className="create-intent-modal__title">
              Сформулируйте задачу в одном окне
            </h3>
            <p id={descriptionId} className="create-intent-modal__description">
              Добавьте текст, теги и изображения. Клик вне окна закроет форму.
            </p>
          </div>
          <button
            type="button"
            className="create-intent-modal__close"
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
          className="create-intent-form"
        >
          <textarea
            className="create-intent-form__textarea"
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
          <input
            className="create-intent-form__tags"
            placeholder="Теги через запятую (опционально)"
            value={tags}
            onChange={(e) => {
              setTags(e.target.value);
            }}
            aria-label="Теги intent"
          />
          <label className="create-intent-form__file-picker">
            <input
              type="file"
              accept="image/*"
              multiple
              onChange={(e) => {
                addFiles(e.currentTarget.files);
                e.currentTarget.value = "";
              }}
            />
            <span className="create-intent-form__file-picker-label">
              <ImagePlus aria-hidden size={16} strokeWidth={2} />
              Приложить изображения
            </span>
            <span className="create-intent-form__file-picker-hint">
              До 10 файлов, каждый до 10 МБ. Можно вставить картинку из буфера.
            </span>
          </label>
          {files.length > 0 ? (
            <ul
              className="create-intent-form__files"
              aria-label="Выбранные файлы"
            >
              {files.map((file, index) => (
                <li
                  key={`${file.name}-${String(file.lastModified)}-${String(index)}`}
                  className="create-intent-form__file"
                >
                  <span className="create-intent-form__file-name">
                    {file.name}
                  </span>
                  <span className="create-intent-form__file-size">
                    {formatBytes(file.size)}
                  </span>
                  <button
                    type="button"
                    className="create-intent-form__file-remove"
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
            <p role="alert" className="edit-text-form__error">
              {error}
            </p>
          ) : null}
          <div className="edit-text-form__actions create-intent-form__actions">
            <Button
              type="submit"
              variant="primary"
              disabled={busy || text.trim().length === 0}
            >
              {busy ? "Создаём…" : "Создать intent"}
            </Button>
            <Button
              type="button"
              onClick={() => {
                close();
              }}
              disabled={busy}
            >
              Отмена
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

function filesFromClipboard(clipboard: DataTransfer): File[] {
  const result: File[] = [];
  const fallbackName = "clipboard-image.png";

  for (const item of Array.from(clipboard.items)) {
    if (item.kind !== "file" || !item.type.startsWith("image/")) continue;

    const file = item.getAsFile();
    if (!file) continue;

    result.push(
      file.name
        ? file
        : new File([file], fallbackName, {
            type: file.type,
            lastModified: file.lastModified
          })
    );
  }

  return result;
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
