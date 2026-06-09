import { Loader2, Send, X } from "lucide-react";
import { useEffect, useState } from "react";

import {
  clearDraft,
  loadDraft,
  saveDraft,
  submitReviewComment,
  type ReviewCommentAnchorShas,
  type ReviewCommentSide
} from "@/entities/review-workspace";
import { HttpError } from "@/shared/api";
import { Button } from "@/shared/ui";

export interface ComposerAnchor {
  path: string;
  previousPath: string | null;
  side: ReviewCommentSide;
  line: number;
}

interface ReviewInlineComposerProps {
  intentId: string;
  bindingId: string;
  anchor: ComposerAnchor;
  shas: ReviewCommentAnchorShas;
  onCancel: () => void;
  onSubmitted: () => void;
}

function describeError(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 422) {
      return "Якорь строки устарел или не принят провайдером. Обнови diff и попробуй снова.";
    }
    if (err.status === 401 || err.status === 403) {
      return "Нет доступа к провайдеру для отправки комментария.";
    }
    return `Не удалось отправить (${String(err.status)}).`;
  }
  return "Не удалось отправить комментарий.";
}

export function ReviewInlineComposer({
  intentId,
  bindingId,
  anchor,
  shas,
  onCancel,
  onSubmitted
}: ReviewInlineComposerProps) {
  const draftAnchor = {
    bindingId,
    path: anchor.path,
    side: anchor.side,
    line: anchor.line
  };
  const [value, setValue] = useState(() => loadDraft(draftAnchor));
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    saveDraft(draftAnchor, value);
    // draftAnchor пересоздаётся на каждый рендер, но завязка нужна только на
    // содержимое якоря, не на ссылку.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value, bindingId, anchor.path, anchor.side, anchor.line]);

  const submit = () => {
    if (value.trim().length === 0 || submitting) return;
    setSubmitting(true);
    setError(null);
    void (async () => {
      try {
        await submitReviewComment(intentId, bindingId, {
          content: value,
          path: anchor.path,
          previous_path: anchor.previousPath ?? undefined,
          side: anchor.side,
          line: anchor.line,
          commit_sha: shas.commit_sha,
          base_sha: shas.base_sha,
          start_sha: shas.start_sha
        });
        clearDraft(draftAnchor);
        onSubmitted();
      } catch (err) {
        setError(describeError(err));
        setSubmitting(false);
      }
    })();
  };

  return (
    <div className="flex flex-col gap-2 border-l-2 border-primary/40 bg-base-200/60 px-3 py-2">
      <textarea
        autoFocus
        rows={3}
        value={value}
        disabled={submitting}
        onChange={(e) => {
          setValue(e.target.value);
        }}
        onKeyDown={(e) => {
          if ((e.metaKey || e.ctrlKey) && e.key === "Enter") submit();
          if (e.key === "Escape") onCancel();
        }}
        placeholder="Inline-комментарий — отправится прямо в провайдер"
        className="w-full resize-y rounded-md border border-base-300 bg-base-100 px-2 py-1.5 font-sans text-[13px] leading-relaxed text-base-content focus:border-primary focus:outline-none"
      />
      {error !== null ? (
        <p role="alert" className="m-0 text-[11px] text-error">
          {error}
        </p>
      ) : null}
      <div className="flex items-center justify-end gap-2">
        <Button
          icon={<X aria-hidden size={13} strokeWidth={2} />}
          onClick={onCancel}
          disabled={submitting}
        >
          Отмена
        </Button>
        <Button
          variant="primary"
          disabled={submitting || value.trim().length === 0}
          icon={
            submitting ? (
              <Loader2
                aria-hidden
                size={13}
                strokeWidth={2}
                className="animate-spin"
              />
            ) : (
              <Send aria-hidden size={13} strokeWidth={2} />
            )
          }
          onClick={submit}
        >
          {submitting ? "Отправляем…" : "Отправить"}
        </Button>
      </div>
    </div>
  );
}
