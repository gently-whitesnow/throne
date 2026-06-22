import { Check, Copy, X } from "lucide-react";
import { useState } from "react";

import type { IntentDetail } from "@/entities/intent";
import { DeleteIntentButton } from "@/features/delete-intent";
import { CleanupOnDoneToggle } from "@/features/set-intent-cleanup-on-done";
import { SetIntentStatusForm } from "@/features/set-intent-status";
import { formatRelativeTime } from "@/shared/lib";
import { Button } from "@/shared/ui";

interface IntentDetailHeaderProps {
  intent: IntentDetail;
  editing: boolean;
  onEdit: () => void;
  onStatusSaved: (next: IntentDetail) => void;
  onClose: () => void;
  onDeleted: () => void;
}

export function IntentDetailHeader({
  intent,
  editing,
  onEdit,
  onStatusSaved,
  onClose,
  onDeleted
}: IntentDetailHeaderProps) {
  const [copied, setCopied] = useState(false);

  const copyId = () => {
    void (async () => {
      try {
        await navigator.clipboard.writeText(intent.id);
        setCopied(true);
        window.setTimeout(() => {
          setCopied(false);
        }, 1500);
      } catch {
        setCopied(false);
      }
    })();
  };

  const title = firstLine(intent.text) || intent.id;
  const updatedDate = new Date(intent.updated_at);

  return (
    <header className="flex flex-shrink-0 items-start justify-between gap-4 border-b border-base-300 px-6 py-3.5">
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <h1 className="m-0 min-w-0 break-words text-lg font-semibold leading-snug text-base-content">
          {title}
        </h1>
        <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1.5 text-[11px] text-base-content/60">
          <SetIntentStatusForm intent={intent} onSaved={onStatusSaved} />
          <CleanupOnDoneToggle intent={intent} onSaved={onStatusSaved} />
          <span aria-hidden className="h-3.5 w-px flex-shrink-0 bg-base-300" />
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
          <IntentIdChip id={intent.id} copied={copied} onCopy={copyId} />
        </div>
      </div>
      <div className="flex flex-shrink-0 items-center gap-2">
        {!editing && (
          <Button variant="primary" onClick={onEdit}>
            Редактировать
          </Button>
        )}
        <DeleteIntentButton intentId={intent.id} onDeleted={onDeleted} />
        <button
          type="button"
          aria-label="Закрыть панель"
          title="Закрыть"
          onClick={onClose}
          className="inline-flex h-8 w-8 items-center justify-center rounded-md text-base-content/60 transition-colors hover:bg-base-200 hover:text-base-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
        >
          <X aria-hidden size={16} strokeWidth={2} />
        </button>
      </div>
    </header>
  );
}

interface IntentIdChipProps {
  id: string;
  copied: boolean;
  onCopy: () => void;
}

function IntentIdChip({ id, copied, onCopy }: IntentIdChipProps) {
  return (
    <button
      type="button"
      aria-label={
        copied ? "Идентификатор скопирован" : `Скопировать id интента ${id}`
      }
      title={copied ? "Скопировано" : `Скопировать id: ${id}`}
      onClick={onCopy}
      className="group inline-flex h-6 flex-shrink-0 items-center gap-1.5 rounded-md border border-base-300/80 bg-base-200/40 pl-2 pr-1.5 font-mono text-[11px] leading-none text-base-content/55 transition-colors hover:border-base-300 hover:bg-base-200 hover:text-base-content/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
    >
      <span className="tabular-nums">{shortId(id)}</span>
      {copied ? (
        <Check
          aria-hidden
          size={12}
          strokeWidth={2.5}
          className="text-success"
        />
      ) : (
        <Copy
          aria-hidden
          size={12}
          strokeWidth={2}
          className="opacity-50 transition-opacity group-hover:opacity-100"
        />
      )}
    </button>
  );
}

function shortId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

function firstLine(text: string): string {
  const line = text.split(/\r?\n/, 1)[0] ?? "";
  return line.length > 80 ? `${line.slice(0, 80)}…` : line;
}
