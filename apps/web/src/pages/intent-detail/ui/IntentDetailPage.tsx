import { useQueryClient } from "@tanstack/react-query";
import { Check, Copy, MessagesSquare, Play, X } from "lucide-react";
import { useEffect, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";

import { intentsQueryKeys, useIntent } from "@/entities/intent";
import { DeleteIntentButton } from "@/features/delete-intent";
import { IntentAttachmentsPanel } from "@/features/manage-intent-attachments";
import { ReplaceIntentTextForm } from "@/features/replace-intent-text";
import { SetIntentStatusForm } from "@/features/set-intent-status";
import { IntentTagsInline } from "@/features/set-intent-tags";
import { HttpError } from "@/shared/api";
import { formatRelativeTime } from "@/shared/lib";
import { useRealtimeEvent } from "@/shared/realtime";
import { Button } from "@/shared/ui";
import { IntentActivityTimeline } from "@/widgets/intent-activity-timeline";
import { IntentLinksSection } from "@/widgets/intent-links-section";
import { PullRequestCommentsSection } from "@/widgets/pull-request-comments";
import { RepositoryBindingsList } from "@/widgets/repository-bindings-list";

export function IntentDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const intentQuery = useIntent(id || null);
  const [editing, setEditing] = useState(false);
  const [copiedAction, setCopiedAction] = useState<
    "id" | "execute" | "interview" | null
  >(null);

  const copyToClipboard = (
    text: string,
    action: "id" | "execute" | "interview"
  ) => {
    void (async () => {
      try {
        await navigator.clipboard.writeText(text);
        setCopiedAction(action);
        window.setTimeout(() => {
          setCopiedAction((current) => (current === action ? null : current));
        }, 1500);
      } catch {
        setCopiedAction(null);
      }
    })();
  };

  useEffect(() => {
    setEditing(false);
  }, [id]);

  useRealtimeEvent("intent.deleted", (payload) => {
    if (payload.intent_id === id) {
      void navigate("/intents");
    }
  });

  if (!id || intentQuery.isPending) {
    return (
      <p className="px-6 py-4 text-[13px] text-base-content/60">Загрузка…</p>
    );
  }
  if (intentQuery.isError) {
    const err = intentQuery.error;
    const message =
      err instanceof HttpError
        ? err.status === 404
          ? "Intent не найден."
          : `Ошибка загрузки (${String(err.status)}).`
        : "Ошибка загрузки.";
    return (
      <p role="alert" className="px-6 py-4 text-[13px] text-error">
        {message}
      </p>
    );
  }

  const intent = intentQuery.data;
  const title = firstLine(intent.text) || intent.id;
  const updatedDate = new Date(intent.updated_at);

  return (
    <>
      <header className="flex flex-shrink-0 items-start justify-between gap-4 border-b border-base-300 px-6 py-3.5">
        <div className="flex min-w-0 flex-col gap-2">
          <div className="flex min-w-0 items-start gap-2">
            <h1 className="m-0 min-w-0 break-words text-lg font-semibold leading-snug text-base-content">
              {title}
            </h1>
            <button
              type="button"
              aria-label={
                copiedAction === "id"
                  ? "Идентификатор скопирован"
                  : "Скопировать id интента"
              }
              title={
                copiedAction === "id"
                  ? "Скопировано"
                  : `Скопировать id: ${intent.id}`
              }
              onClick={() => {
                copyToClipboard(intent.id, "id");
              }}
              className="mt-1 inline-flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-md text-base-content/50 transition-colors hover:bg-base-200 hover:text-base-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
            >
              {copiedAction === "id" ? (
                <Check aria-hidden size={14} strokeWidth={2} />
              ) : (
                <Copy aria-hidden size={14} strokeWidth={2} />
              )}
            </button>
            <button
              type="button"
              aria-label={
                copiedAction === "execute"
                  ? "Команда «выполни intent» скопирована"
                  : "Скопировать команду «выполни intent»"
              }
              title={
                copiedAction === "execute"
                  ? "Скопировано"
                  : `Скопировать: Используя mcp throne, прочитай бандл work и выполни интент ${intent.id}`
              }
              onClick={() => {
                copyToClipboard(
                  `Используя mcp throne, прочитай бандл work и выполни интент ${intent.id}`,
                  "execute"
                );
              }}
              className="mt-1 inline-flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-md text-base-content/50 transition-colors hover:bg-base-200 hover:text-base-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
            >
              {copiedAction === "execute" ? (
                <Check aria-hidden size={14} strokeWidth={2} />
              ) : (
                <Play aria-hidden size={14} strokeWidth={2} />
              )}
            </button>
            <button
              type="button"
              aria-label={
                copiedAction === "interview"
                  ? "Команда «проведи интервью» скопирована"
                  : "Скопировать команду «проведи интервью»"
              }
              title={
                copiedAction === "interview"
                  ? "Скопировано"
                  : `Скопировать: Используя mcp throne, прочитай бандл interview и проведи интервью по интенту ${intent.id}`
              }
              onClick={() => {
                copyToClipboard(
                  `Используя mcp throne, прочитай бандл interview и проведи интервью по интенту ${intent.id}`,
                  "interview"
                );
              }}
              className="mt-1 inline-flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-md text-base-content/50 transition-colors hover:bg-base-200 hover:text-base-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
            >
              {copiedAction === "interview" ? (
                <Check aria-hidden size={14} strokeWidth={2} />
              ) : (
                <MessagesSquare aria-hidden size={14} strokeWidth={2} />
              )}
            </button>
          </div>
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5 text-[11px] text-base-content/60">
            <SetIntentStatusForm
              intent={intent}
              onSaved={(next) => {
                queryClient.setQueryData(
                  intentsQueryKeys.detail(intent.id),
                  next
                );
              }}
            />
            <span className="tabular-nums font-semibold text-base-content/70">
              v{intent.current_version}
            </span>
            <span className="text-base-content/30">·</span>
            <time
              dateTime={intent.updated_at}
              title={updatedDate.toLocaleString()}
              className="tabular-nums"
            >
              изменён {formatRelativeTime(updatedDate)}
            </time>
          </div>
        </div>
        <div className="flex flex-shrink-0 items-center gap-2">
          {!editing && (
            <Button
              variant="primary"
              onClick={() => {
                setEditing(true);
              }}
            >
              Редактировать
            </Button>
          )}
          <DeleteIntentButton
            intentId={intent.id}
            onDeleted={() => {
              void navigate("/intents");
            }}
          />
          <button
            type="button"
            aria-label="Закрыть панель"
            title="Закрыть"
            onClick={() => {
              void navigate({
                pathname: "/intents",
                search: location.search
              });
            }}
            className="inline-flex h-8 w-8 items-center justify-center rounded-md text-base-content/60 transition-colors hover:bg-base-200 hover:text-base-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
          >
            <X aria-hidden size={16} strokeWidth={2} />
          </button>
        </div>
      </header>

      <div className="flex min-h-0 flex-1 overflow-hidden">
        <div
          className={
            editing
              ? "flex min-h-0 flex-1 flex-col gap-3 px-6 pb-4 pt-4"
              : "min-h-0 flex-1 overflow-y-auto px-6 pb-8 pt-4"
          }
        >
          <IntentTagsInline
            intent={intent}
            onSaved={(next) => {
              queryClient.setQueryData(
                intentsQueryKeys.detail(intent.id),
                next
              );
            }}
          />
          {editing ? (
            <ReplaceIntentTextForm
              intent={intent}
              onSaved={(next) => {
                queryClient.setQueryData(
                  intentsQueryKeys.detail(intent.id),
                  next
                );
                setEditing(false);
              }}
              onCancel={() => {
                setEditing(false);
              }}
            />
          ) : (
            <>
              <pre className="m-0 whitespace-pre-wrap break-words font-mono text-[13px] leading-relaxed text-base-content">
                {intent.text}
              </pre>
              <IntentAttachmentsPanel intentId={intent.id} />
              <RepositoryBindingsList intentId={intent.id} />
              <PullRequestCommentsSection intentId={intent.id} />
              <IntentLinksSection intentId={intent.id} />
              <section className="mt-6 flex flex-col gap-2">
                <h2 className="m-0 text-sm font-semibold text-base-content">
                  Активность
                </h2>
                <IntentActivityTimeline intentId={intent.id} />
              </section>
            </>
          )}
        </div>
      </div>
    </>
  );
}

function firstLine(text: string): string {
  const line = text.split(/\r?\n/, 1)[0] ?? "";
  return line.length > 80 ? `${line.slice(0, 80)}…` : line;
}
