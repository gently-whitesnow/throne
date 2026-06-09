import { ExternalLink, Loader2, RefreshCw, Sparkles } from "lucide-react";
import { useState } from "react";

import type { PullRequestComment } from "@/entities/pull-request-comment";
import { formatRelativeTime } from "@/shared/lib";
import { Button } from "@/shared/ui";

type RailTab = "comments" | "context";

interface ReviewRightRailProps {
  comments: PullRequestComment[];
  commentsLoading: boolean;
  commentsError: Error | null;
  syncing: boolean;
  onSync: () => void;
}

export function ReviewRightRail({
  comments,
  commentsLoading,
  commentsError,
  syncing,
  onSync
}: ReviewRightRailProps) {
  const [tab, setTab] = useState<RailTab>("comments");

  return (
    <aside
      aria-label="Контекст ревью"
      className="flex h-full min-h-0 w-80 shrink-0 flex-col border-l border-base-300 bg-base-100"
    >
      <div role="tablist" className="flex border-b border-base-300">
        <TabButton
          active={tab === "comments"}
          onClick={() => {
            setTab("comments");
          }}
          label={`Комментарии${comments.length > 0 ? ` · ${String(comments.length)}` : ""}`}
        />
        <TabButton
          active={tab === "context"}
          onClick={() => {
            setTab("context");
          }}
          label="Контекст"
        />
      </div>

      {tab === "comments" ? (
        <CommentsTab
          comments={comments}
          loading={commentsLoading}
          error={commentsError}
          syncing={syncing}
          onSync={onSync}
        />
      ) : (
        <ContextTab />
      )}
    </aside>
  );
}

function TabButton({
  active,
  onClick,
  label
}: {
  active: boolean;
  onClick: () => void;
  label: string;
}) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={`flex-1 px-3 py-2 text-[12px] font-medium ${
        active
          ? "border-b-2 border-primary text-primary"
          : "text-base-content/60 hover:text-base-content"
      }`}
    >
      {label}
    </button>
  );
}

function CommentsTab({
  comments,
  loading,
  error,
  syncing,
  onSync
}: {
  comments: PullRequestComment[];
  loading: boolean;
  error: Error | null;
  syncing: boolean;
  onSync: () => void;
}) {
  return (
    <>
      <div className="flex items-center justify-between gap-2 border-b border-base-300 px-3 py-2">
        <span className="text-[11px] text-base-content/50">
          Источник истины — провайдер
        </span>
        <Button
          aria-label="Обновить комментарии"
          disabled={syncing}
          icon={
            syncing ? (
              <Loader2
                aria-hidden
                size={13}
                strokeWidth={2}
                className="animate-spin"
              />
            ) : (
              <RefreshCw aria-hidden size={13} strokeWidth={2} />
            )
          }
          onClick={onSync}
        >
          {syncing ? "Обновляем…" : "Обновить"}
        </Button>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto p-3">
        {error !== null ? (
          <p role="alert" className="m-0 text-xs text-error">
            Не удалось загрузить комментарии: {error.message}
          </p>
        ) : comments.length === 0 ? (
          <p className="m-0 text-xs text-base-content/60">
            {loading ? "Загружаем комментарии…" : "Комментариев пока нет."}
          </p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-2 p-0">
            {comments.map((c) => (
              <CommentItem key={c.id} comment={c} />
            ))}
          </ul>
        )}
      </div>
    </>
  );
}

function CommentItem({ comment }: { comment: PullRequestComment }) {
  return (
    <li className="flex flex-col gap-1 rounded-md border border-base-300 bg-base-100 px-3 py-2">
      <div className="flex flex-wrap items-baseline gap-x-2 text-[11px]">
        <span className="font-semibold text-base-content">
          {comment.author_login}
        </span>
        <time
          dateTime={comment.created_at}
          className="tabular-nums text-base-content/60"
        >
          {formatRelativeTime(new Date(comment.created_at))}
        </time>
        {comment.html_url != null ? (
          <a
            href={comment.html_url}
            target="_blank"
            rel="noreferrer"
            aria-label="Открыть комментарий у провайдера"
            className="ml-auto inline-flex items-center gap-1 text-base-content/60 hover:text-primary"
          >
            <ExternalLink aria-hidden size={11} strokeWidth={2} />
          </a>
        ) : null}
      </div>
      {comment.path != null ? (
        <span
          className="truncate font-mono text-[10px] text-base-content/60"
          title={comment.path}
        >
          {comment.path}
        </span>
      ) : null}
      <p className="m-0 whitespace-pre-wrap break-words text-[12px] leading-relaxed text-base-content">
        {comment.body}
      </p>
    </li>
  );
}

function ContextTab() {
  return (
    <div className="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 px-6 py-8 text-center">
      <Sparkles
        aria-hidden
        size={20}
        strokeWidth={1.75}
        className="text-base-content/40"
      />
      <p className="m-0 text-xs text-base-content/60">
        Здесь появятся AI-рекомендации к ревью и схема репозитория.
      </p>
    </div>
  );
}
