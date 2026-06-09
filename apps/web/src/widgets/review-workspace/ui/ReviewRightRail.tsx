import { Loader2, RefreshCw, Sparkles } from "lucide-react";
import { useState } from "react";

import type { PullRequestComment } from "@/entities/pull-request-comment";
import { Button } from "@/shared/ui";

import { ReviewCommentCard, type CommentActions } from "./ReviewCommentCard";
import { ReviewDescriptionTab } from "./ReviewDescriptionTab";

type RailTab = "description" | "comments" | "context";

interface ReviewRightRailProps {
  intentId: string;
  bindingId: string;
  comments: PullRequestComment[];
  commentsLoading: boolean;
  commentsError: Error | null;
  syncing: boolean;
  onSync: () => void;
  commentActions: CommentActions;
  onJump: (comment: PullRequestComment) => void;
}

export function ReviewRightRail({
  intentId,
  bindingId,
  comments,
  commentsLoading,
  commentsError,
  syncing,
  onSync,
  commentActions,
  onJump
}: ReviewRightRailProps) {
  const [tab, setTab] = useState<RailTab>("description");

  return (
    <aside
      aria-label="Контекст ревью"
      className="flex h-full min-h-0 flex-col bg-base-100"
    >
      <div role="tablist" className="flex border-b border-base-300">
        <TabButton
          active={tab === "description"}
          onClick={() => {
            setTab("description");
          }}
          label="Описание"
        />
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

      {tab === "description" ? (
        <ReviewDescriptionTab intentId={intentId} bindingId={bindingId} />
      ) : tab === "comments" ? (
        <CommentsTab
          comments={comments}
          loading={commentsLoading}
          error={commentsError}
          syncing={syncing}
          onSync={onSync}
          commentActions={commentActions}
          onJump={onJump}
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
  onSync,
  commentActions,
  onJump
}: {
  comments: PullRequestComment[];
  loading: boolean;
  error: Error | null;
  syncing: boolean;
  onSync: () => void;
  commentActions: CommentActions;
  onJump: (comment: PullRequestComment) => void;
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
              <li key={c.id}>
                <ReviewCommentCard
                  comment={c}
                  actions={commentActions}
                  onJump={onJump}
                />
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
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
