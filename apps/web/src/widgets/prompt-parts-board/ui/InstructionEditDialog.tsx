import { X } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { createPortal } from "react-dom";
import { useQueryClient } from "@tanstack/react-query";

import type { InstructionDetail } from "@/entities/instruction";
import { ReplaceInstructionTextForm } from "@/features/replace-instruction-text";
import { Button } from "@/shared/ui";

import { bundlesTreeQueryKeys } from "../model/use-bundles-tree";
import type { ProjectedInstruction } from "../model/composition";

interface InstructionEditDialogProps {
  instruction: ProjectedInstruction;
  onClose: () => void;
}

/**
 * View / edit dialog for a projected mandatory instruction. System-scope is
 * read-only; user-scope reuses ReplaceInstructionTextForm (create or replace).
 */
export function InstructionEditDialog({
  instruction,
  onClose
}: InstructionEditDialogProps) {
  const titleId = useId();
  const qc = useQueryClient();
  const [editing, setEditing] = useState(false);

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

  const handleSaved = () => {
    void qc.invalidateQueries({ queryKey: bundlesTreeQueryKeys.current() });
    onClose();
  };

  const detail: InstructionDetail = {
    id: instruction.instructionId ?? "",
    kind: instruction.kind,
    current_version: instruction.currentVersion,
    text: instruction.text,
    created_at: new Date(0).toISOString(),
    updated_at: new Date(0).toISOString()
  };

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
              {instruction.scope === "user"
                ? "User-инструкция"
                : "System-инструкция"}
            </p>
            <h2
              id={titleId}
              className="m-0 font-mono text-xl font-bold tracking-tight"
            >
              {instruction.kind}
            </h2>
            <p className="m-0 mt-1.5 text-[13px] text-base-content/70">
              Режимы: {instruction.modes.join(", ")}
            </p>
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
          {instruction.scope !== "user" ? (
            <ReadOnlyText
              hint="System-инструкция. Меняется только через манифест."
              text={instruction.text}
            />
          ) : editing || !instruction.present ? (
            <ReplaceInstructionTextForm
              instruction={detail}
              onSaved={handleSaved}
              onCancel={
                instruction.present
                  ? () => {
                      setEditing(false);
                    }
                  : onClose
              }
            />
          ) : (
            <>
              <pre className="m-0 whitespace-pre-wrap break-words rounded-md border border-base-300 bg-base-200 px-4 py-3.5 font-mono text-xs leading-relaxed">
                {instruction.text}
              </pre>
              <div className="flex justify-end gap-2">
                <Button onClick={onClose}>Закрыть</Button>
                <Button
                  variant="primary"
                  onClick={() => {
                    setEditing(true);
                  }}
                >
                  Редактировать
                </Button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>,
    document.body
  );
}

function ReadOnlyText({ text, hint }: { text: string; hint?: string }) {
  return (
    <section className="flex flex-col gap-1.5">
      {hint ? <p className="m-0 text-xs text-base-content/60">{hint}</p> : null}
      <pre className="m-0 whitespace-pre-wrap break-words rounded-md border border-base-300 bg-base-200 px-4 py-3.5 font-mono text-xs leading-relaxed">
        {text}
      </pre>
    </section>
  );
}
