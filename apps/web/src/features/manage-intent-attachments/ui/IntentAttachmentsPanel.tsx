import { useQueryClient } from "@tanstack/react-query";
import { Image, ImagePlus, Paperclip, X } from "lucide-react";
import { useEffect, useState } from "react";

import {
  intentsQueryKeys,
  useIntentAttachments,
  type IntentAttachment
} from "@/entities/intent";
import {
  HttpError,
  INTENT_ATTACHMENTS_CHANGED_EVENT,
  apiUrl,
  httpDelete,
  httpGetBlob,
  httpPostForm,
  intentsEndpoints
} from "@/shared/api";
import { filesFromClipboard } from "@/shared/lib";
import { SectionHeading } from "@/shared/ui";

const MAX_ATTACHMENTS = 10;
const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;
const EMPTY_ATTACHMENTS: readonly IntentAttachment[] = [];

interface IntentAttachmentsPanelProps {
  intentId: string;
}

export function IntentAttachmentsPanel({
  intentId
}: IntentAttachmentsPanelProps) {
  const queryClient = useQueryClient();
  const attachmentsQuery = useIntentAttachments(intentId);
  const attachments: readonly IntentAttachment[] =
    attachmentsQuery.data ?? EMPTY_ATTACHMENTS;
  const [busyDeleteId, setBusyDeleteId] = useState<string | null>(null);
  const [uploadingCount, setUploadingCount] = useState(0);
  const [actionError, setActionError] = useState<string | null>(null);
  const [previews, setPreviews] = useState<Partial<Record<string, string>>>({});

  const invalidateAttachments = () => {
    void queryClient.invalidateQueries({
      queryKey: intentsQueryKeys.attachments(intentId)
    });
  };

  useEffect(() => {
    const controller = new AbortController();
    const urls: string[] = [];
    setPreviews({});

    for (const attachment of attachments) {
      if (!attachment.content_type.startsWith("image/")) continue;
      httpGetBlob(
        intentsEndpoints.downloadIntentAttachment(intentId, attachment.id),
        controller.signal
      )
        .then((blob) => {
          const url = URL.createObjectURL(blob);
          urls.push(url);
          setPreviews((current) => ({ ...current, [attachment.id]: url }));
        })
        .catch(() => {
          if (controller.signal.aborted) return;
        });
    }

    return () => {
      controller.abort();
      for (const url of urls) URL.revokeObjectURL(url);
    };
  }, [attachments, intentId]);

  useEffect(() => {
    const listener = (event: Event) => {
      const detail = (event as CustomEvent<AttachmentChangedDetail>).detail;
      if (detail.intentId !== intentId) return;
      if (detail.error) {
        setActionError(detail.error);
      }
      void queryClient.invalidateQueries({
        queryKey: intentsQueryKeys.attachments(intentId)
      });
    };

    window.addEventListener(INTENT_ATTACHMENTS_CHANGED_EVENT, listener);
    return () => {
      window.removeEventListener(INTENT_ATTACHMENTS_CHANGED_EVENT, listener);
    };
  }, [intentId, queryClient]);

  const deleteAttachment = async (attachment: IntentAttachment) => {
    if (!window.confirm(`Удалить вложение «${attachment.file_name}»?`)) {
      return;
    }

    setBusyDeleteId(attachment.id);
    setActionError(null);
    try {
      await httpDelete(
        intentsEndpoints.deleteIntentAttachment(intentId, attachment.id)
      );
      invalidateAttachments();
    } catch (err: unknown) {
      const message =
        err instanceof HttpError
          ? `Не удалось удалить вложение (${String(err.status)}).`
          : "Не удалось удалить вложение.";
      setActionError(message);
    } finally {
      setBusyDeleteId(null);
    }
  };

  const uploadFiles = async (nextFiles: Iterable<File> | null) => {
    if (!nextFiles || !attachmentsQuery.isSuccess) return;

    const files = Array.from(nextFiles);
    const accepted: File[] = [];
    const problems: string[] = [];
    const remainingSlots = MAX_ATTACHMENTS - attachments.length;

    for (const file of files) {
      if (accepted.length >= remainingSlots) {
        problems.push(
          `Можно приложить максимум ${String(MAX_ATTACHMENTS)} файлов.`
        );
        break;
      }
      if (file.size > MAX_ATTACHMENT_BYTES) {
        problems.push(`${file.name}: файл больше 10 МБ.`);
        continue;
      }
      accepted.push(file);
    }

    if (accepted.length === 0) {
      setActionError(problems.length > 0 ? unique(problems).join(" ") : null);
      return;
    }

    setUploadingCount(accepted.length);
    setActionError(problems.length > 0 ? unique(problems).join(" ") : null);

    for (const file of accepted) {
      const form = new FormData();
      form.append("file", file, file.name);
      try {
        await httpPostForm<IntentAttachment>(
          intentsEndpoints.uploadIntentAttachment(intentId),
          form
        );
        invalidateAttachments();
      } catch (err: unknown) {
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить ${file.name} (${String(err.status)}).`
            : `Не удалось загрузить ${file.name}.`;
        setActionError(message);
      } finally {
        setUploadingCount((count) => Math.max(0, count - 1));
      }
    }
  };

  const canUpload =
    attachmentsQuery.isSuccess &&
    attachments.length < MAX_ATTACHMENTS &&
    uploadingCount === 0;

  const loadErrorMessage = attachmentsQuery.isError
    ? attachmentsQuery.error instanceof HttpError
      ? `Не удалось загрузить вложения (${String(attachmentsQuery.error.status)}).`
      : "Не удалось загрузить вложения."
    : null;

  return (
    <section
      className="flex flex-col gap-3 focus-within:outline-2 focus-within:outline-primary/40 focus-within:outline-offset-2"
      aria-labelledby="attachments-title"
      tabIndex={0}
      onPaste={(event) => {
        const pasted = filesFromClipboard(event.clipboardData);
        if (pasted.length === 0) return;
        event.preventDefault();
        void uploadFiles(pasted);
      }}
    >
      <div className="flex flex-col gap-1">
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
          actions={
            <label
              className={`btn btn-sm btn-soft gap-1.5 ${
                canUpload ? "" : "btn-disabled"
              }`}
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
          }
        />
        <p className="m-0 pl-6 text-xs text-base-content/60">
          До 10 файлов по 10 МБ; изображения показываются превью. Можно вставить
          картинку из буфера.
        </p>
      </div>

      {attachmentsQuery.isPending ? (
        <p className="m-0 text-sm text-base-content/60">Загружаем вложения…</p>
      ) : null}
      {loadErrorMessage ? (
        <p role="alert" className="m-0 text-sm text-error">
          {loadErrorMessage}
        </p>
      ) : null}
      {actionError ? (
        <p role="alert" className="m-0 text-sm text-error">
          {actionError}
        </p>
      ) : null}
      {attachmentsQuery.isSuccess && attachments.length === 0 ? (
        <p className="m-0 text-sm text-base-content/60">Пока нет вложений.</p>
      ) : null}
      {attachmentsQuery.isSuccess && attachments.length > 0 ? (
        <ul
          className="m-0 grid list-none gap-3 p-0 [grid-template-columns:repeat(auto-fill,minmax(180px,1fr))]"
          aria-label="Вложения intent"
        >
          {attachments.map((attachment) => {
            const preview = previews[attachment.id];
            const contentUrl =
              preview ??
              apiUrl(
                intentsEndpoints.downloadIntentAttachment(
                  intentId,
                  attachment.id
                )
              );
            const deleting = busyDeleteId === attachment.id;
            return (
              <li
                key={attachment.id}
                className="group relative flex flex-col gap-2 rounded-lg border border-base-300 bg-base-100 p-2.5"
              >
                <a
                  className="flex aspect-[4/3] items-center justify-center overflow-hidden rounded-md bg-base-200 text-base-content/60 focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-2"
                  href={contentUrl}
                  target="_blank"
                  rel="noreferrer"
                  aria-label={`Открыть ${attachment.file_name}`}
                >
                  {preview ? (
                    <img
                      src={preview}
                      alt={attachment.file_name}
                      className="h-full w-full object-cover"
                    />
                  ) : (
                    <span className="inline-flex items-center justify-center">
                      <Image aria-hidden size={24} strokeWidth={1.8} />
                    </span>
                  )}
                </a>
                <button
                  type="button"
                  onClick={() => {
                    void deleteAttachment(attachment);
                  }}
                  disabled={deleting}
                  aria-label={`Удалить ${attachment.file_name}`}
                  className="absolute right-3 top-3 inline-flex h-7 w-7 items-center justify-center rounded-full border border-base-300 bg-base-100/95 text-base-content/70 opacity-0 shadow-sm transition-opacity hover:bg-error hover:text-error-content focus-visible:opacity-100 group-hover:opacity-100 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <X aria-hidden size={14} strokeWidth={2.5} />
                </button>
                <div className="flex min-w-0 flex-col gap-px">
                  <span className="truncate text-[13px] font-semibold">
                    {attachment.file_name}
                  </span>
                  <span className="text-[11px] tabular-nums text-base-content/60">
                    {formatBytes(attachment.size_bytes)}
                  </span>
                </div>
              </li>
            );
          })}
        </ul>
      ) : null}
    </section>
  );
}

interface AttachmentChangedDetail {
  intentId: string;
  error?: string;
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
