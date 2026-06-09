import {
  ExternalLink,
  GitMerge,
  GitPullRequest,
  Loader2,
  RefreshCw
} from "lucide-react";

import {
  useReviewPullRequestQuery,
  type PullRequestHeader
} from "@/entities/review-workspace";
import { Button, MarkdownView } from "@/shared/ui";

type PullRequestState = NonNullable<PullRequestHeader["state"]>;

const STATE_LABEL: Record<PullRequestState, string> = {
  open: "Открыт",
  merged: "Смержен",
  closed: "Закрыт"
};

const STATE_TONE: Record<PullRequestState, string> = {
  open: "bg-success/15 text-success",
  merged: "bg-primary/15 text-primary",
  closed: "bg-error/15 text-error"
};

interface ReviewDescriptionTabProps {
  intentId: string;
  bindingId: string;
}

// Mounted only while the tab is selected, so the read-through fires on open
// (and again on tab re-entry once the 60s cache goes stale).
export function ReviewDescriptionTab({
  intentId,
  bindingId
}: ReviewDescriptionTabProps) {
  const query = useReviewPullRequestQuery(intentId, bindingId, true);
  const header = query.data ?? null;

  return (
    <>
      <div className="flex items-center justify-between gap-2 border-b border-base-300 px-3 py-2">
        <span className="text-[11px] text-base-content/50">
          Источник истины — провайдер
        </span>
        <Button
          aria-label="Обновить описание"
          disabled={query.isFetching}
          icon={
            query.isFetching ? (
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
          onClick={() => void query.refetch()}
        >
          {query.isFetching ? "Обновляем…" : "Обновить"}
        </Button>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto p-3">
        {query.isError ? (
          <p role="alert" className="m-0 text-xs text-error">
            Не удалось загрузить описание: {query.error.message}
          </p>
        ) : header === null ? (
          <p className="m-0 text-xs text-base-content/60">
            Загружаем описание…
          </p>
        ) : (
          <DescriptionBody header={header} />
        )}
      </div>
    </>
  );
}

function DescriptionBody({ header }: { header: PullRequestHeader }) {
  return (
    <article className="flex flex-col gap-3">
      <header className="flex flex-col gap-2">
        <div className="flex items-start gap-2">
          <GitPullRequest
            aria-hidden
            size={16}
            strokeWidth={2}
            className="mt-0.5 shrink-0 text-base-content/50"
          />
          <h2 className="m-0 text-sm font-semibold leading-snug text-base-content">
            {header.title ?? `#${String(header.number)}`}
            <span className="ml-1.5 font-normal text-base-content/50">
              #{header.number}
            </span>
          </h2>
        </div>

        <div className="flex flex-wrap items-center gap-2 text-[11px]">
          <span
            className={`rounded-full px-2 py-0.5 font-medium ${STATE_TONE[header.state]}`}
          >
            {STATE_LABEL[header.state]}
          </span>
          {header.author_login != null ? (
            <span className="flex items-center gap-1 text-base-content/70">
              {header.author_avatar_url != null ? (
                <img
                  src={header.author_avatar_url}
                  alt=""
                  className="h-4 w-4 rounded-full"
                />
              ) : null}
              {header.author_login}
            </span>
          ) : null}
          {header.head_ref != null && header.base_ref != null ? (
            <span className="flex items-center gap-1 font-mono text-base-content/60">
              <GitMerge aria-hidden size={12} strokeWidth={2} />
              {header.head_ref} → {header.base_ref}
            </span>
          ) : null}
          {header.html_url != null ? (
            <a
              href={header.html_url}
              target="_blank"
              rel="noreferrer"
              className="flex items-center gap-1 text-primary hover:underline"
            >
              <ExternalLink aria-hidden size={12} strokeWidth={2} />
              Открыть
            </a>
          ) : null}
        </div>
      </header>

      {header.body != null && header.body.trim() !== "" ? (
        <MarkdownView markdown={header.body} className="text-xs" />
      ) : (
        <p className="m-0 text-xs text-base-content/50">Описание пустое.</p>
      )}
    </article>
  );
}
