import { useQueryClient } from "@tanstack/react-query";
import {
  AlertCircle,
  GitPullRequest,
  Loader2,
  MessageSquare,
  RefreshCw
} from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import {
  intentRepositoriesQueryKeys,
  pullRequestStateMeta,
  repositoryFullName,
  type RepositoryBinding
} from "@/entities/repository-binding";
import {
  compareComments,
  syncPullRequest,
  usePullRequestComments
} from "@/entities/pull-request-comment";
import { errorMessage } from "@/shared/lib";
import { Button } from "@/shared/ui";

import { PullRequestCommentItem } from "./PullRequestCommentItem";

interface PullRequestCommentsCardProps {
  intentId: string;
  binding: RepositoryBinding;
}

/**
 * Карточка PR-комментариев одного binding'а.
 *
 * - Список читает entity hook `usePullRequestComments` — он уже подписан на
 *   `intent.pr_comment_added`, поэтому фоновое обновление работает без рефреша.
 * - Кнопка «Обновить» дёргает `POST .../sync` (ADR-0024): эндпоинт
 *   синхронный и возвращает свежий список, а realtime-фанаут гарантирует, что
 *   другие открытые вкладки получат те же дельты.
 *
 * Виртуализация не нужна — surface ограничен ≤50 review-комментов на PR.
 */
export function PullRequestCommentsCard({
  intentId,
  binding
}: PullRequestCommentsCardProps) {
  const queryClient = useQueryClient();
  const { comments, isLoading, error, refresh } = usePullRequestComments(
    intentId,
    binding.id
  );
  const [syncing, setSyncing] = useState(false);
  const [syncError, setSyncError] = useState<string | null>(null);
  const [syncedPrState, setSyncedPrState] = useState(
    binding.pull_request_state
  );

  useEffect(() => {
    setSyncedPrState(binding.pull_request_state);
  }, [binding.pull_request_state]);

  const handleRefresh = useCallback(() => {
    setSyncError(null);
    setSyncing(true);
    void (async () => {
      try {
        const result = await syncPullRequest(intentId, binding.id);
        // Server fans out `intent.pr_comment_added` for the deltas, но мы
        // дополнительно перечитываем список — это безопаснее, чем полагаться на
        // реалтайм-доставку до завершения POST-запроса.
        queryClient.setQueryData<RepositoryBinding[]>(
          intentRepositoriesQueryKeys.list(intentId),
          (prev) =>
            prev?.map((b) =>
              b.id === result.binding_id
                ? {
                    ...b,
                    pull_request_state:
                      result.pull_request_state !== undefined
                        ? result.pull_request_state
                        : b.pull_request_state,
                    last_synced_at:
                      result.last_synced_at !== undefined
                        ? result.last_synced_at
                        : b.last_synced_at
                  }
                : b
            )
        );
        void queryClient.invalidateQueries({
          queryKey: intentRepositoriesQueryKeys.list(intentId)
        });
        if (result.pull_request_state !== undefined) {
          setSyncedPrState(result.pull_request_state);
        }
        refresh();
      } catch (err) {
        setSyncError(
          errorMessage(err, {
            base: "Не удалось обновить",
            fallback: "Не удалось обновить комментарии."
          })
        );
      } finally {
        setSyncing(false);
      }
    })();
  }, [intentId, binding.id, refresh, queryClient]);

  const fullName = repositoryFullName(binding);
  const prNumber = binding.pull_request_number;
  const prState = syncedPrState;
  const stateMeta = prState != null ? pullRequestStateMeta[prState] : null;
  const sorted = [...comments].sort(compareComments);

  return (
    <article
      data-testid={`pr-comments-card-${binding.id}`}
      className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 px-4 py-3"
    >
      <header className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex min-w-0 flex-wrap items-center gap-2 text-xs">
          <span
            aria-hidden
            className="inline-flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary"
          >
            <GitPullRequest size={13} strokeWidth={2.25} />
          </span>
          <span className="truncate font-mono text-sm font-semibold text-base-content">
            {fullName}
          </span>
          {prNumber !== undefined ? (
            <span
              className="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 font-semibold"
              style={
                stateMeta
                  ? { backgroundColor: stateMeta.surface, color: stateMeta.ink }
                  : {
                      backgroundColor: "var(--color-status-neutral-surface)",
                      color: "var(--color-status-neutral-ink)"
                    }
              }
              data-testid={`pr-comments-pr-${binding.id}`}
            >
              PR #{prNumber}
              {stateMeta ? (
                <span className="text-[10px]">· {stateMeta.label}</span>
              ) : null}
            </span>
          ) : null}
          <span
            className="inline-flex items-center gap-1 text-[11px] text-base-content/60"
            data-testid={`pr-comments-count-${binding.id}`}
          >
            <MessageSquare aria-hidden size={11} strokeWidth={2} />
            {sorted.length}
          </span>
        </div>
        <Button
          aria-label={`Обновить комментарии PR #${String(prNumber ?? "")}`}
          disabled={syncing}
          icon={
            syncing ? (
              <Loader2
                aria-hidden
                size={14}
                strokeWidth={2}
                className="animate-spin"
              />
            ) : (
              <RefreshCw aria-hidden size={14} strokeWidth={2} />
            )
          }
          onClick={handleRefresh}
        >
          {syncing ? "Обновляем…" : "Обновить"}
        </Button>
      </header>

      {syncError !== null ? (
        <p role="alert" className="m-0 text-xs text-error">
          {syncError}
        </p>
      ) : null}

      <Body
        bindingId={binding.id}
        isLoading={isLoading}
        error={error}
        comments={sorted}
      />
    </article>
  );
}

interface BodyProps {
  bindingId: string;
  isLoading: boolean;
  error: Error | null;
  comments: ReturnType<typeof usePullRequestComments>["comments"];
}

function Body({ bindingId, isLoading, error, comments }: BodyProps) {
  if (error !== null) {
    return (
      <p
        role="alert"
        className="m-0 flex items-start gap-2 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-xs text-error"
      >
        <AlertCircle aria-hidden size={14} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось загрузить комментарии: {error.message}</span>
      </p>
    );
  }
  if (comments.length === 0) {
    if (isLoading) {
      return (
        <p className="m-0 text-xs text-base-content/60">
          Загружаем комментарии…
        </p>
      );
    }
    return (
      <p
        data-testid={`pr-comments-empty-${bindingId}`}
        className="m-0 text-xs text-base-content/60"
      >
        Комментариев пока нет.
      </p>
    );
  }
  return (
    <ul
      data-testid={`pr-comments-list-${bindingId}`}
      className="m-0 flex list-none flex-col gap-2 p-0"
    >
      {comments.map((c) => (
        <PullRequestCommentItem key={c.id} comment={c} />
      ))}
    </ul>
  );
}
