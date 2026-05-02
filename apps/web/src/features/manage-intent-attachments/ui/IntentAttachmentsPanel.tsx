import { Image, ImagePlus, Trash2 } from "lucide-react";
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
import { useRealtimeEvent } from "@/shared/realtime";
import { Button } from "@/shared/ui";

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

  const uploadFiles = async (nextFiles: FileList | null) => {
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
    <section className="attachments-panel" aria-labelledby="attachments-title">
      <div className="attachments-panel__header">
        <div>
          <h2 id="attachments-title" className="attachments-panel__title">
            Вложения
          </h2>
          <p className="attachments-panel__hint">
            Можно приложить до 10 файлов по 10 МБ; изображения показываются
            превью.
          </p>
        </div>
        <div className="attachments-panel__actions">
          {state.kind === "ready" ? (
            <span className="attachments-panel__count">
              {String(state.attachments.length)}/10
            </span>
          ) : null}
          <label
            className={`attachments-panel__upload${
              canUpload ? "" : " attachments-panel__upload--disabled"
            }`}
          >
            <input
              type="file"
              multiple
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
        <p className="attachments-panel__empty">Загружаем вложения…</p>
      ) : null}
      {state.kind === "error" ? (
        <p role="alert" className="edit-text-form__error">
          {state.message}
        </p>
      ) : null}
      {actionError ? (
        <p role="alert" className="edit-text-form__error">
          {actionError}
        </p>
      ) : null}
      {state.kind === "ready" && state.attachments.length === 0 ? (
        <p className="attachments-panel__empty">Пока нет вложений.</p>
      ) : null}
      {state.kind === "ready" && state.attachments.length > 0 ? (
        <ul className="attachments-grid" aria-label="Вложения intent">
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
            return (
              <li key={attachment.id} className="attachments-grid__item">
                <a
                  className="attachments-grid__preview"
                  href={contentUrl}
                  target="_blank"
                  rel="noreferrer"
                  aria-label={`Открыть ${attachment.file_name}`}
                >
                  {preview ? (
                    <img src={preview} alt={attachment.file_name} />
                  ) : (
                    <span className="attachments-grid__placeholder">
                      <Image aria-hidden size={24} strokeWidth={1.8} />
                    </span>
                  )}
                </a>
                <div className="attachments-grid__meta">
                  <span className="attachments-grid__name">
                    {attachment.file_name}
                  </span>
                  <span className="attachments-grid__size">
                    {formatBytes(attachment.size_bytes)}
                  </span>
                </div>
                <Button
                  type="button"
                  onClick={() => {
                    void deleteAttachment(attachment);
                  }}
                  disabled={busyDeleteId === attachment.id}
                  aria-label={`Удалить ${attachment.file_name}`}
                  icon={<Trash2 aria-hidden size={14} strokeWidth={2} />}
                >
                  {busyDeleteId === attachment.id ? "Удаляем…" : "Удалить"}
                </Button>
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
