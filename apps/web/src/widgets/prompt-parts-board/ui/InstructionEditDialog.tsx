import { X } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { createPortal } from "react-dom";
import { useQueryClient } from "@tanstack/react-query";

import { useReplacePromptPartText } from "@/entities/prompt-part";
import { HttpError } from "@/shared/api";
import { computeMinimalTextDelta } from "@/shared/lib";
import { Button } from "@/shared/ui";

import { bundlesTreeQueryKeys } from "../model/use-bundles-tree";
import type { ProjectedInstruction } from "../model/composition";

interface InstructionEditDialogProps {
  instruction: ProjectedInstruction;
  onClose: () => void;
}

/**
 * View / edit dialog for a projected mandatory part. System-scope is read-only
 * (seeded from the manifest). Present user-scope parts can have their text
 * edited in place via replace-text; missing user-scope parts are seeded from
 * the manifest on first apply and are shown read-only here.
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

  const isEditableUser =
    instruction.scope === "user" &&
    instruction.present &&
    instruction.prompt_part_id !== null;

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
                ? "User-часть (mandatory)"
                : "System-часть (mandatory)"}
            </p>
            <h2
              id={titleId}
              className="m-0 font-mono text-xl font-bold tracking-tight"
            >
              {instruction.key}
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
              hint="System-часть. Меняется только через манифест."
              text={instruction.text}
            />
          ) : !isEditableUser ? (
            <ReadOnlyText
              hint="User-часть ещё не создана — она засевается из манифеста при первом применении патча."
              text={instruction.text}
            />
          ) : editing ? (
            <ReplaceTextForm
              partId={instruction.prompt_part_id ?? ""}
              currentVersion={instruction.currentVersion}
              text={instruction.text}
              onSaved={handleSaved}
              onCancel={() => {
                setEditing(false);
              }}
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

interface ReplaceTextFormProps {
  partId: string;
  currentVersion: number;
  text: string;
  onSaved: () => void;
  onCancel: () => void;
}

function ReplaceTextForm({
  partId,
  currentVersion,
  text,
  onSaved,
  onCancel
}: ReplaceTextFormProps) {
  const [draft, setDraft] = useState(text);
  const [error, setError] = useState<string | null>(null);
  const replaceText = useReplacePromptPartText();

  const submit = () => {
    if (replaceText.isPending) return;
    const delta = computeMinimalTextDelta(text, draft);
    if (delta === null) {
      onCancel();
      return;
    }
    setError(null);
    replaceText.mutate(
      {
        id: partId,
        expectedVersion: currentVersion,
        oldText: delta.oldText,
        newText: delta.newText
      },
      {
        onSuccess: () => {
          onSaved();
        },
        onError: (err: unknown) => {
          setError(formatReplaceError(err));
        }
      }
    );
  };

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        submit();
      }}
      className="flex flex-col gap-3"
    >
      <textarea
        className="textarea textarea-bordered min-h-80 w-full font-mono text-[13px] leading-relaxed"
        value={draft}
        onChange={(e) => {
          setDraft(e.target.value);
        }}
        rows={20}
        aria-label="Текст части"
      />
      {error ? (
        <p role="alert" className="m-0 text-sm text-error">
          {error}
        </p>
      ) : null}
      <div className="flex gap-2">
        <Button
          type="submit"
          variant="primary"
          disabled={replaceText.isPending}
        >
          {replaceText.isPending ? "Сохраняем…" : "Сохранить"}
        </Button>
        <Button
          type="button"
          onClick={onCancel}
          disabled={replaceText.isPending}
        >
          Отмена
        </Button>
      </div>
    </form>
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

function formatReplaceError(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 409) {
      return "Версия устарела — обновите страницу и повторите правку.";
    }
    if (err.status === 422) {
      return "Не удалось применить правку (текст не совпал).";
    }
    return `Ошибка сохранения (${String(err.status)}).`;
  }
  return "Не удалось сохранить.";
}
