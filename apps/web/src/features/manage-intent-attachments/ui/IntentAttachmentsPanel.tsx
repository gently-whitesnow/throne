import { Image, ImagePlus, X } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";

import type { IntentAttachment } from "@/entities/intent";
import {
  HttpError,
  INTENT_ATTACHMENTS_CHANGED_EVENT,
  apiUrl,
  httpDelete,
  httpGet,
  httpGetBlob,
  httpPostForm,
  intentsEndpoints
} from "@/shared/api";
import { filesFromClipboard } from "@/shared/lib";
import { useRealtimeEvent } from "@/shared/realtime";

const MAX_ATTACHMENTS = 10;
const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;

interface IntentAttachmentsPanelProps {
  intentId: string;
}

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; attachments: IntentAttachment[] }
  | { kind: "error"; message: string };

export function IntentAttachmentsPanel({
  intentId
}: IntentAttachmentsPanelProps) {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);
  const [busyDeleteId, setBusyDeleteId] = useState<string | null>(null);
  const [uploadingCount, setUploadingCount] = useState(0);
  const [actionError, setActionError] = useState<string | null>(null);
  const [previews, setPreviews] = useState<Partial<Record<string, string>>>({});

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: "loading" });
    setActionError(null);
    httpGet<IntentAttachment[]>(
      intentsEndpoints.listIntentAttachments(intentId),
      controller.signal
    )
      .then((attachments) => {
        setState({ kind: "ready", attachments });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить вложения (${String(err.status)}).`
            : "Не удалось загрузить вложения.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, [intentId, reloadKey]);

  const attachments = useMemo(
    () => (state.kind === "ready" ? state.attachments : []),
    [state]
  );

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
      setReloadKey((key) => key + 1);
    };

    window.addEventListener(INTENT_ATTACHMENTS_CHANGED_EVENT, listener);
    return () => {
      window.removeEventListener(INTENT_ATTACHMENTS_CHANGED_EVENT, listener);
    };
  }, [intentId]);

  const onAttachmentChanged = useCallback(
    (payload: { intent_id: string }) => {
      if (payload.intent_id === intentId) {
        setReloadKey((key) => key + 1);
      }
    },
    [intentId]
  );

  useRealtimeEvent("intent.attachment_added", onAttachmentChanged);
  useRealtimeEvent("intent.attachment_deleted", onAttachmentChanged);

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
      setReloadKey((key) => key + 1);
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
    if (!nextFiles || state.kind !== "ready") return;

    const files = Array.from(nextFiles);
    const accepted: File[] = [];
    const problems: string[] = [];
    const remainingSlots = MAX_ATTACHMENTS - state.attachments.length;

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
        setReloadKey((key) => key + 1);
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
    state.kind === "ready" &&
    state.attachments.length < MAX_ATTACHMENTS &&
    uploadingCount === 0;

  return (
    <section
      className="mt-5 rounded-md border-t border-base-300 pt-4 focus-within:outline-2 focus-within:outline-primary/40 focus-within:outline-offset-2"
      aria-labelledby="attachments-title"
      tabIndex={0}
      onPaste={(event) => {
        const pasted = filesFromClipboard(event.clipboardData);
        if (pasted.length === 0) return;
        event.preventDefault();
        void uploadFiles(pasted);
      }}
    >
      <div className="mb-3 flex items-start justify-between gap-3">
        <div>
          <h2
            id="attachments-title"
            className="m-0 text-base font-bold text-base-content"
          >
            Вложения
          </h2>
          <p className="mt-1 text-xs text-base-content/60">
            До 10 файлов по 10 МБ; изображения показываются превью. Можно
            вставить картинку из буфера.
          </p>
        </div>
        <div className="flex flex-shrink-0 items-center gap-2">
          {state.kind === "ready" ? (
            <span className="badge badge-sm bg-primary/10 text-primary">
              {String(state.attachments.length)}/10
            </span>
          ) : null}
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
        </div>
      </div>

      {state.kind === "loading" ? (
        <p className="m-0 text-sm text-base-content/60">Загружаем вложения…</p>
      ) : null}
      {state.kind === "error" ? (
        <p role="alert" className="m-0 text-sm text-error">
          {state.message}
        </p>
      ) : null}
      {actionError ? (
        <p role="alert" className="m-0 text-sm text-error">
          {actionError}
        </p>
      ) : null}
      {state.kind === "ready" && state.attachments.length === 0 ? (
        <p className="m-0 text-sm text-base-content/60">Пока нет вложений.</p>
      ) : null}
      {state.kind === "ready" && state.attachments.length > 0 ? (
        <ul
          className="m-0 grid list-none gap-3 p-0 [grid-template-columns:repeat(auto-fill,minmax(180px,1fr))]"
          aria-label="Вложения intent"
        >
          {state.attachments.map((attachment) => {
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
