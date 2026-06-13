import { X } from "lucide-react";
import { useEffect, useId } from "react";
import { createPortal } from "react-dom";

import type { PromptPartListItem } from "@/entities/prompt-part";

import { CreatePromptPartBody } from "./CreatePromptPartBody";
import { EditPromptPartBody } from "./EditPromptPartBody";
import { SystemPartBody } from "./SystemPartBody";

export type PromptPartDialogTarget =
  | { mode: "create" }
  | { mode: "detail"; part: PromptPartListItem };

interface PromptPartDetailDialogProps {
  target: PromptPartDialogTarget;
  onClose: () => void;
}

export function PromptPartDetailDialog({
  target,
  onClose
}: PromptPartDetailDialogProps) {
  const titleId = useId();

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
      }
    };
    const { overflow } = document.body.style;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = overflow;
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [onClose]);

  const isSystem = target.mode === "detail" && target.part.scope === "system";

  return createPortal(
    <div
      className="modal modal-open"
      role="presentation"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="modal-box flex max-h-[min(880px,calc(100vh-48px))] w-full max-w-3xl flex-col border border-base-300 bg-base-100"
      >
        <header className="mb-4 flex justify-between gap-4">
          <div>
            <p className="m-0 mb-1 text-[11px] font-bold uppercase tracking-wider text-primary">
              {target.mode === "create"
                ? "Новая часть"
                : isSystem
                  ? "System-часть"
                  : "User-часть"}
            </p>
            <h2
              id={titleId}
              className="m-0 font-mono text-xl font-bold tracking-tight"
            >
              {target.mode === "create" ? "Новая часть" : target.part.key}
            </h2>
          </div>
          <button
            type="button"
            className="btn btn-sm btn-circle btn-ghost"
            onClick={onClose}
            aria-label="Закрыть"
          >
            <X aria-hidden size={16} strokeWidth={2} />
          </button>
        </header>

        <div className="flex flex-col gap-4 overflow-y-auto">
          {target.mode === "create" ? (
            <CreatePromptPartBody onClose={onClose} />
          ) : isSystem ? (
            <SystemPartBody part={target.part} onClose={onClose} />
          ) : (
            <EditPromptPartBody part={target.part} onClose={onClose} />
          )}
        </div>
      </div>
    </div>,
    document.body
  );
}
