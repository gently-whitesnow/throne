import { ImagePlus, Plus, X } from "lucide-react";
import type { ReactNode } from "react";
import { useId, useState } from "react";

import type { IntentDetail, IntentStatus } from "@/entities/intent";
import { useTagPicker } from "@/entities/tag";
import { httpPost, intentsEndpoints } from "@/shared/api";
import { errorMessage, filesFromClipboard } from "@/shared/lib";
import { Button, Modal, TagMultiSelect } from "@/shared/ui";

import { collectImageFiles, uploadAttachments } from "../model/attachments";
import { AttachmentList } from "./AttachmentList";

interface CreateIntentButtonProps {
  onCreated?: (intent: IntentDetail) => void;
  initialTags?: readonly string[];
  initialStatus?: IntentStatus;
  trigger?: (props: { open: () => void; isOpen: boolean }) => ReactNode;
}

export function CreateIntentButton({
  onCreated,
  initialTags,
  initialStatus,
  trigger
}: CreateIntentButtonProps) {
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [tags, setTags] = useState<string[]>(() => [...(initialTags ?? [])]);
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const tagPicker = useTagPicker();
  const titleId = useId();
  const descriptionId = useId();

  const reset = () => {
    setText("");
    setTags([...(initialTags ?? [])]);
    setFiles([]);
    setError(null);
  };

  const close = () => {
    if (busy) return;
    reset();
    setOpen(false);
  };

  const addFiles = (nextFiles: Iterable<File> | null) => {
    if (!nextFiles) return;
    const result = collectImageFiles(files, nextFiles);
    setFiles(result.files);
    setError(result.error);
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
      let finalIntent = created;
      if (initialStatus && initialStatus !== created.status) {
        finalIntent = await httpPost<IntentDetail>(
          intentsEndpoints.setIntentStatus(created.id),
          { status: initialStatus }
        );
      }
      reset();
      setOpen(false);
      onCreated?.(finalIntent);
      if (filesToUpload.length > 0) {
        void uploadAttachments(created.id, filesToUpload);
      }
    } catch (err: unknown) {
      const message = errorMessage(err, { base: "Не удалось создать" });
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  const openDialog = () => {
    setTags([...(initialTags ?? [])]);
    setOpen(true);
  };

  if (!open) {
    if (trigger) {
      return <>{trigger({ open: openDialog, isOpen: false })}</>;
    }
    return (
      <Button
        aria-label="Создать intent"
        icon={<Plus aria-hidden size={18} strokeWidth={2.4} />}
        variant="primary"
        onClick={openDialog}
      >
        Создать
      </Button>
    );
  }

  return (
    <Modal
      onClose={close}
      labelledBy={titleId}
      describedBy={descriptionId}
      boxClassName="max-h-[min(960px,calc(100vh-32px))] w-full max-w-2xl"
    >
      <div className="mb-5 flex items-start justify-between gap-4">
        <div className="flex flex-col gap-2">
          <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
            Новый intent
          </p>
          <h3
            id={titleId}
            className="m-0 text-balance text-xl font-semibold leading-tight"
          >
            Сформулируйте задачу в одном окне
          </h3>
          <p
            id={descriptionId}
            className="m-0 max-w-[46ch] text-pretty text-sm leading-relaxed text-base-content/70"
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
          className="textarea textarea-bordered min-h-[320px] w-full text-base"
          placeholder="Кратко опишите intent"
          value={text}
          onChange={(e) => {
            setText(e.target.value);
          }}
          onPaste={(e) => {
            pasteImages(e.clipboardData);
          }}
          rows={12}
          aria-label="Текст intent"
          autoFocus
        />
        <TagMultiSelect
          value={tags}
          onChange={setTags}
          candidates={tagPicker.candidates}
          query={tagPicker.query}
          onQueryChange={tagPicker.setQuery}
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
        <AttachmentList files={files} busy={busy} onRemove={removeFile} />
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
    </Modal>
  );
}
