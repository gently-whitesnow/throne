import { ImagePlus, Paperclip } from "lucide-react";
import { useEffect, useState } from "react";

import { filesFromClipboard } from "@/shared/lib";
import { SectionHeading } from "@/shared/ui";

import {
  MAX_ATTACHMENTS,
  useAttachmentUploads
} from "../lib/use-attachment-uploads";
import { AttachmentChip } from "./AttachmentChip";

interface IntentAttachmentsPanelProps {
  intentId: string;
}

export function IntentAttachmentsPanel({
  intentId
}: IntentAttachmentsPanelProps) {
  const {
    attachmentsQuery,
    attachments,
    previews,
    busyDeleteId,
    uploadingCount,
    actionError,
    canUpload,
    loadErrorMessage,
    deleteAttachment,
    uploadFiles,
    uploadFilesRef
  } = useAttachmentUploads(intentId);
  const [hovered, setHovered] = useState(false);

  // Наведение мышью + Cmd+V вставляет скриншот без явного фокуса в секции —
  // событие paste летит в document, поэтому слушаем его только пока курсор тут.
  useEffect(() => {
    if (!hovered) return;
    const onPaste = (event: ClipboardEvent) => {
      if (!event.clipboardData) return;
      const pasted = filesFromClipboard(event.clipboardData);
      if (pasted.length === 0) return;
      event.preventDefault();
      void uploadFilesRef.current(pasted);
    };
    window.addEventListener("paste", onPaste);
    return () => {
      window.removeEventListener("paste", onPaste);
    };
  }, [hovered, uploadFilesRef]);

  return (
    <section
      className="flex flex-col gap-2 focus-within:outline-2 focus-within:outline-primary/40 focus-within:outline-offset-2"
      aria-labelledby="attachments-title"
      tabIndex={0}
      onMouseEnter={() => {
        setHovered(true);
      }}
      onMouseLeave={() => {
        setHovered(false);
      }}
      onPaste={(event) => {
        const pasted = filesFromClipboard(event.clipboardData);
        if (pasted.length === 0) return;
        event.preventDefault();
        void uploadFiles(pasted);
      }}
    >
      <SectionHeading
        id="attachments-title"
        icon={<Paperclip size={14} strokeWidth={2} />}
        title="Вложения"
        meta={
          attachmentsQuery.isSuccess ? (
            <span className="rounded-full bg-primary/10 px-1.5 py-0.5 text-[11px] font-semibold tabular-nums text-primary">
              {String(attachments.length)}/10
            </span>
          ) : null
        }
      />

      {attachmentsQuery.isPending ? (
        <p className="m-0 pl-6 text-sm text-base-content/60">
          Загружаем вложения…
        </p>
      ) : null}
      {loadErrorMessage ? (
        <p role="alert" className="m-0 pl-6 text-sm text-error">
          {loadErrorMessage}
        </p>
      ) : null}
      {actionError ? (
        <p role="alert" className="m-0 pl-6 text-sm text-error">
          {actionError}
        </p>
      ) : null}

      {attachmentsQuery.isSuccess ? (
        <ul
          className="m-0 flex list-none flex-wrap items-center gap-2 p-0 pl-6"
          aria-label="Вложения intent"
        >
          {attachments.map((attachment) => (
            <AttachmentChip
              key={attachment.id}
              intentId={intentId}
              attachment={attachment}
              preview={previews[attachment.id]}
              deleting={busyDeleteId === attachment.id}
              onDelete={() => {
                void deleteAttachment(attachment);
              }}
            />
          ))}
          {attachments.length < MAX_ATTACHMENTS ? (
            <li>
              <label
                className={`inline-flex cursor-pointer items-center gap-1.5 rounded-full border border-dashed border-base-300 px-2.5 py-1 text-[12px] font-medium text-base-content/70 transition-colors hover:border-primary hover:text-primary ${
                  canUpload ? "" : "pointer-events-none opacity-50"
                }`}
                title="Нажми, чтобы выбрать файл, или наведи курсор и вставь скриншот (Cmd+V)"
              >
                <input
                  type="file"
                  multiple
                  className="sr-only"
                  disabled={!canUpload}
                  onChange={(e) => {
                    void uploadFiles(e.currentTarget.files);
                    e.currentTarget.value = "";
                  }}
                />
                <ImagePlus aria-hidden size={14} strokeWidth={2} />
                {uploadingCount > 0 ? "Загружаем…" : "Приложить"}
              </label>
            </li>
          ) : null}
        </ul>
      ) : null}
    </section>
  );
}
