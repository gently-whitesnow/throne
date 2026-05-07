import { Download, Trash2 } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import {
  type ChatUpload,
  chatUploadDownloadHref,
  deleteChatUpload,
  fetchChatUploads,
  formatBytes,
  formatDateShort,
  formatDateTimeShort
} from "@/entities/chat-upload";
import { useRealtimeEvent } from "@/shared/realtime";
import { Button } from "@/shared/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: ChatUpload[] }
  | { kind: "error"; message: string };

export function ChatUploadsList() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchChatUploads(controller.signal)
      .then((items) => {
        setState({ kind: "ready", items });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setState({
          kind: "error",
          message:
            err instanceof Error
              ? err.message
              : "Не удалось загрузить архивы переписок."
        });
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey]);

  const reload = useCallback(() => {
    setReloadKey((v) => v + 1);
  }, []);

  useRealtimeEvent("chat_upload.created", reload);
  useRealtimeEvent("chat_upload.deleted", reload);

  const handleDelete = useCallback((upload: ChatUpload) => {
    const ok = window.confirm(
      `Удалить архив ${upload.agent} (${upload.device}) от ${formatDateTimeShort(upload.created_at)}?\nФайл и метаданные будут удалены без возможности восстановления.`
    );
    if (!ok) return;
    setBusyId(upload.id);
    setActionError(null);
    void (async () => {
      try {
        await deleteChatUpload(upload.id);
      } catch (err: unknown) {
        setActionError(
          err instanceof Error ? err.message : "Не удалось удалить архив."
        );
      } finally {
        setBusyId(null);
      }
    })();
  }, []);

  if (state.kind === "loading") {
    return <div className="p-4 text-sm text-base-content/60">Загрузка...</div>;
  }
  if (state.kind === "error") {
    return <div className="p-4 text-sm text-error">{state.message}</div>;
  }

  if (state.items.length === 0) {
    return (
      <div className="flex h-full flex-col items-center justify-center gap-2 p-4 text-sm text-base-content/60">
        <p className="m-0">Архивов пока нет.</p>
        <p className="m-0 max-w-md text-center text-xs">
          Попроси своего агента «отправь историю чатов в Throne» — он соберёт
          zip с manifest и загрузит его сюда.
        </p>
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col">
      {actionError ? (
        <div
          role="alert"
          className="m-3 rounded-md bg-error/10 px-3 py-2 text-sm text-error"
        >
          {actionError}
        </div>
      ) : null}
      <div className="overflow-x-auto">
        <table className="table table-sm">
          <thead>
            <tr>
              <th>Устройство</th>
              <th>Агент</th>
              <th>Период</th>
              <th className="text-right">Диалогов</th>
              <th className="text-right">Размер</th>
              <th>Загружено</th>
              <th className="text-right">Действия</th>
            </tr>
          </thead>
          <tbody>
            {state.items.map((upload) => (
              <tr key={upload.id}>
                <td>
                  <div className="font-medium">
                    {upload.device_display_name ?? upload.device}
                  </div>
                  {upload.device_display_name ? (
                    <div className="text-xs text-base-content/60">
                      {upload.device}
                    </div>
                  ) : null}
                </td>
                <td>
                  <div className="font-medium">{upload.agent}</div>
                  {upload.agent_version ? (
                    <div className="text-xs text-base-content/60">
                      {upload.agent_version}
                    </div>
                  ) : null}
                </td>
                <td className="whitespace-nowrap">
                  {formatDateShort(upload.date_range.from)} —{" "}
                  {formatDateShort(upload.date_range.to)}
                </td>
                <td className="text-right tabular-nums">
                  {String(upload.conversation_count)}
                </td>
                <td className="text-right tabular-nums">
                  {formatBytes(upload.size_bytes)}
                </td>
                <td className="whitespace-nowrap text-xs text-base-content/70">
                  {formatDateTimeShort(upload.created_at)}
                </td>
                <td className="text-right">
                  <div className="flex justify-end gap-2">
                    <a
                      href={chatUploadDownloadHref(upload.id)}
                      title="Скачать"
                      aria-label={`Скачать архив ${upload.id}`}
                      className="inline-flex h-8 w-8 items-center justify-center rounded-md text-base-content/70 hover:bg-base-300/60 hover:text-base-content"
                    >
                      <Download size={16} aria-hidden />
                    </a>
                    <Button
                      type="button"
                      title="Удалить"
                      aria-label={`Удалить архив ${upload.id}`}
                      className="!h-8 !min-h-8 !w-8 !px-0"
                      onClick={() => {
                        handleDelete(upload);
                      }}
                      disabled={busyId === upload.id}
                      icon={<Trash2 size={16} aria-hidden />}
                    />
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
