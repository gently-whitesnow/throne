import { ExternalLink, UserRound } from "lucide-react";

import type { PullRequestComment } from "@/entities/pull-request-comment";
import { formatRelativeTime } from "@/shared/lib";

interface PullRequestCommentItemProps {
  comment: PullRequestComment;
}

/**
 * Один review-комментарий PR.
 *
 * Тело пока рендерим как plain text с сохранением переносов строк — единая
 * markdown-библиотека во фронте не подключена (см. intent text в
 * IntentDetailPage, который тоже идёт через `<pre>` whitespace-pre-wrap).
 * Появится shared markdown-рендер — заменим тело без правок остального виджета.
 */
export function PullRequestCommentItem({
  comment
}: PullRequestCommentItemProps) {
  const createdAt = new Date(comment.created_at);
  return (
    <li
      data-testid={`pr-comment-${comment.id}`}
      className="flex gap-3 rounded-md border border-base-300 bg-base-100 px-3 py-2.5"
    >
      <Avatar
        url={comment.author_avatar_url ?? undefined}
        login={comment.author_login}
      />
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5 text-xs">
          <span className="font-semibold text-base-content">
            {comment.author_login}
          </span>
          <time
            dateTime={comment.created_at}
            title={createdAt.toLocaleString()}
            className="tabular-nums text-base-content/60"
          >
            {formatRelativeTime(createdAt)}
          </time>
          {comment.path != null ? (
            <span
              className="truncate font-mono text-[11px] text-base-content/60"
              title={comment.path}
            >
              · {comment.path}
            </span>
          ) : null}
          {comment.html_url != null ? (
            <a
              href={comment.html_url}
              target="_blank"
              rel="noreferrer"
              className="ml-auto inline-flex items-center gap-1 text-[11px] text-base-content/60 hover:text-primary"
              aria-label="Открыть комментарий на GitHub"
            >
              <ExternalLink aria-hidden size={11} strokeWidth={2} />
              GitHub
            </a>
          ) : null}
        </div>
        <pre
          data-testid={`pr-comment-body-${comment.id}`}
          className="m-0 whitespace-pre-wrap break-words font-sans text-[13px] leading-relaxed text-base-content"
        >
          {comment.body}
        </pre>
      </div>
    </li>
  );
}

function Avatar({ url, login }: { url?: string; login: string }) {
  if (url !== undefined && url.length > 0) {
    return (
      <img
        src={url}
        alt=""
        loading="lazy"
        width={28}
        height={28}
        className="h-7 w-7 flex-shrink-0 rounded-full border border-base-300 object-cover"
      />
    );
  }
  return (
    <span
      aria-hidden
      title={login}
      className="inline-flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full bg-base-200 text-base-content/60"
    >
      <UserRound size={14} strokeWidth={2} />
    </span>
  );
}
