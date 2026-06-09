import {
  CheckCircle2,
  ChevronDown,
  ExternalLink,
  Loader2,
  RotateCcw,
  Trash2
} from "lucide-react";
import { useState } from "react";

import type { PullRequestComment } from "@/entities/pull-request-comment";
import { formatRelativeTime } from "@/shared/lib";

export interface CommentActions {
  onDelete: (comment: PullRequestComment) => Promise<void>;
  onToggleResolved: (comment: PullRequestComment) => Promise<void>;
}

/**
 * One review comment. Resolved comments render collapsed (author + file + a
 * resolved chip) and expand on click to reveal the body and actions; unresolved
 * comments render expanded. Used both inline under a diff row and in the rail.
 */
export function ReviewCommentCard({
  comment,
  actions,
  onJump,
  showFile = true
}: {
  comment: PullRequestComment;
  actions: CommentActions;
  /** When provided, the header becomes a click-to-line affordance. */
  onJump?: (comment: PullRequestComment) => void;
  showFile?: boolean;
}) {
  const isResolved = comment.resolved === true;
  const [expanded, setExpanded] = useState(!isResolved);
  const [busy, setBusy] = useState<"delete" | "resolve" | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const canResolve = comment.thread_id != null && comment.thread_id.length > 0;
  const open = isResolved ? expanded : true;

  const runDelete = () => {
    setBusy("delete");
    setActionError(null);
    void actions
      .onDelete(comment)
      .catch(() => {
        setActionError("Не удалось удалить комментарий.");
      })
      .finally(() => {
        setBusy(null);
        setConfirmingDelete(false);
      });
  };

  const runToggle = () => {
    setBusy("resolve");
    setActionError(null);
    void actions
      .onToggleResolved(comment)
      .catch(() => {
        setActionError("Не удалось изменить статус треда.");
      })
      .finally(() => {
        setBusy(null);
      });
  };

  return (
    <div className="flex flex-col gap-1 rounded-md border border-base-300 bg-base-100 px-3 py-2">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px]">
        <HeaderIdentity comment={comment} onJump={onJump} />
        {isResolved ? (
          <span className="inline-flex items-center gap-1 rounded-full bg-success-soft px-1.5 py-0.5 text-[10px] font-medium text-success">
            <CheckCircle2 aria-hidden size={11} strokeWidth={2} />
            Решено
          </span>
        ) : null}
        <div className="ml-auto flex items-center gap-1.5">
          {comment.html_url != null ? (
            <a
              href={comment.html_url}
              target="_blank"
              rel="noreferrer"
              aria-label="Открыть комментарий у провайдера"
              className="text-base-content/60 hover:text-primary"
            >
              <ExternalLink aria-hidden size={11} strokeWidth={2} />
            </a>
          ) : null}
          {isResolved ? (
            <button
              type="button"
              aria-label={
                open ? "Свернуть комментарий" : "Развернуть комментарий"
              }
              aria-expanded={open}
              onClick={() => {
                setExpanded((v) => !v);
              }}
              className="text-base-content/60 hover:text-primary"
            >
              <ChevronDown
                aria-hidden
                size={13}
                strokeWidth={2}
                className={
                  open
                    ? "rotate-180 transition-transform"
                    : "transition-transform"
                }
              />
            </button>
          ) : null}
        </div>
      </div>

      {showFile && comment.path != null ? (
        <span
          className="truncate font-mono text-[10px] text-base-content/60"
          title={comment.path}
        >
          {comment.path}
          {comment.line != null ? `:${String(comment.line)}` : ""}
        </span>
      ) : null}

      {open ? (
        <>
          <p className="m-0 whitespace-pre-wrap break-words text-[12px] leading-relaxed text-base-content">
            {comment.body}
          </p>
          {actionError !== null ? (
            <p role="alert" className="m-0 text-[11px] text-error">
              {actionError}
            </p>
          ) : null}
          <div className="mt-0.5 flex items-center gap-2 text-[11px]">
            {canResolve ? (
              <ActionButton
                onClick={runToggle}
                disabled={busy !== null}
                spinning={busy === "resolve"}
                icon={
                  isResolved ? (
                    <RotateCcw aria-hidden size={12} strokeWidth={2} />
                  ) : (
                    <CheckCircle2 aria-hidden size={12} strokeWidth={2} />
                  )
                }
              >
                {isResolved ? "Переоткрыть" : "Решить"}
              </ActionButton>
            ) : null}
            {confirmingDelete ? (
              <span className="inline-flex items-center gap-1.5">
                <span className="text-base-content/60">Удалить?</span>
                <button
                  type="button"
                  onClick={runDelete}
                  disabled={busy !== null}
                  className="font-medium text-error hover:underline disabled:opacity-50"
                >
                  Да
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setConfirmingDelete(false);
                  }}
                  disabled={busy !== null}
                  className="text-base-content/60 hover:underline"
                >
                  Нет
                </button>
              </span>
            ) : (
              <ActionButton
                onClick={() => {
                  setConfirmingDelete(true);
                }}
                disabled={busy !== null}
                spinning={busy === "delete"}
                tone="error"
                icon={<Trash2 aria-hidden size={12} strokeWidth={2} />}
              >
                Удалить
              </ActionButton>
            )}
          </div>
        </>
      ) : null}
    </div>
  );
}

function HeaderIdentity({
  comment,
  onJump
}: {
  comment: PullRequestComment;
  onJump?: (comment: PullRequestComment) => void;
}) {
  const identity = (
    <>
      <span className="font-semibold text-base-content">
        {comment.author_login}
      </span>
      <time
        dateTime={comment.created_at}
        className="tabular-nums text-base-content/60"
      >
        {formatRelativeTime(new Date(comment.created_at))}
      </time>
    </>
  );

  if (onJump === undefined) {
    return <span className="flex items-baseline gap-x-2">{identity}</span>;
  }
  return (
    <button
      type="button"
      onClick={() => {
        onJump(comment);
      }}
      className="flex items-baseline gap-x-2 text-left hover:[&>span]:text-primary"
    >
      {identity}
    </button>
  );
}

function ActionButton({
  children,
  onClick,
  disabled,
  spinning,
  icon,
  tone = "default"
}: {
  children: string;
  onClick: () => void;
  disabled: boolean;
  spinning: boolean;
  icon: React.ReactNode;
  tone?: "default" | "error";
}) {
  const color =
    tone === "error"
      ? "text-base-content/60 hover:text-error"
      : "text-base-content/60 hover:text-primary";
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className={`inline-flex items-center gap-1 ${color} disabled:opacity-50`}
    >
      {spinning ? (
        <Loader2
          aria-hidden
          size={12}
          strokeWidth={2}
          className="animate-spin"
        />
      ) : (
        icon
      )}
      {children}
    </button>
  );
}
