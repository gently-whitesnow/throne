import { Check, Copy, MessagesSquare, Play, X } from "lucide-react";
import { useState } from "react";

import type { IntentDetail } from "@/entities/intent";
import { DeleteIntentButton } from "@/features/delete-intent";
import { CleanupOnDoneToggle } from "@/features/set-intent-cleanup-on-done";
import { SetIntentStatusForm } from "@/features/set-intent-status";
import { formatRelativeTime } from "@/shared/lib";
import { Button } from "@/shared/ui";

type CopyAction = "id" | "execute" | "interview";

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
  const [copiedAction, setCopiedAction] = useState<CopyAction | null>(null);

  const copyToClipboard = (text: string, action: CopyAction) => {
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

  const title = firstLine(intent.text) || intent.id;
  const updatedDate = new Date(intent.updated_at);

  return (
    <header className="flex flex-shrink-0 items-start justify-between gap-4 border-b border-base-300 px-6 py-3.5">
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <h1 className="m-0 min-w-0 break-words text-lg font-semibold leading-snug text-base-content">
          {title}
        </h1>
        <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1.5 text-[11px] text-base-content/60">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5">
            <SetIntentStatusForm intent={intent} onSaved={onStatusSaved} />
            <CleanupOnDoneToggle intent={intent} onSaved={onStatusSaved} />
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
          <div className="flex flex-shrink-0 items-center gap-1">
            <CopyButton
              active={copiedAction === "id"}
              ariaLabel={
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
              icon={<Copy aria-hidden size={14} strokeWidth={2} />}
            />
            <CopyButton
              active={copiedAction === "execute"}
              ariaLabel={
                copiedAction === "execute"
                  ? "Команда «выполни intent» скопирована"
                  : "Скопировать команду «выполни intent»"
              }
              title={
                copiedAction === "execute"
                  ? "Скопировано"
                  : `Скопировать: В Throne запусти work-сессию по интенту ${intent.id}`
              }
              onClick={() => {
                copyToClipboard(
                  `В Throne запусти work-сессию по интенту ${intent.id}`,
                  "execute"
                );
              }}
              icon={<Play aria-hidden size={14} strokeWidth={2} />}
            />
            <CopyButton
              active={copiedAction === "interview"}
              ariaLabel={
                copiedAction === "interview"
                  ? "Команда «проведи интервью» скопирована"
                  : "Скопировать команду «проведи интервью»"
              }
              title={
                copiedAction === "interview"
                  ? "Скопировано"
                  : `Скопировать: В Throne запусти interview-сессию по интенту ${intent.id}`
              }
              onClick={() => {
                copyToClipboard(
                  `В Throne запусти interview-сессию по интенту ${intent.id}`,
                  "interview"
                );
              }}
              icon={<MessagesSquare aria-hidden size={14} strokeWidth={2} />}
            />
          </div>
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

interface CopyButtonProps {
  active: boolean;
  ariaLabel: string;
  title: string;
  onClick: () => void;
  icon: React.ReactNode;
}

function CopyButton({
  active,
  ariaLabel,
  title,
  onClick,
  icon
}: CopyButtonProps) {
  return (
    <button
      type="button"
      aria-label={ariaLabel}
      title={title}
      onClick={onClick}
      className="inline-flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-md text-base-content/50 transition-colors hover:bg-base-200 hover:text-base-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
    >
      {active ? <Check aria-hidden size={14} strokeWidth={2} /> : icon}
    </button>
  );
}

function firstLine(text: string): string {
  const line = text.split(/\r?\n/, 1)[0] ?? "";
  return line.length > 80 ? `${line.slice(0, 80)}…` : line;
}
