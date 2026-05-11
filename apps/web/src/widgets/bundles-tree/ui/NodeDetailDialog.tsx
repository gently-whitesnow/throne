import { X } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { createPortal } from "react-dom";

import type { InstructionDetail } from "@/entities/instruction";
import { ReplaceInstructionTextForm } from "@/features/replace-instruction-text";
import { Button } from "@/shared/ui";

import type { BundleEntryNode, BundleNode, SelectedNode } from "../model/types";

interface NodeDetailDialogProps {
  selection: SelectedNode;
  onClose: () => void;
  onSaved: () => void;
}

export function NodeDetailDialog({
  selection,
  onClose,
  onSaved
}: NodeDetailDialogProps) {
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
              {selectionEyebrow(selection)}
            </p>
            <h2
              id={titleId}
              className="m-0 font-mono text-xl font-bold tracking-tight"
            >
              {selectionTitle(selection)}
            </h2>
            {selectionSubtitle(selection) ? (
              <p className="m-0 mt-1.5 text-[13px] text-base-content/70">
                {selectionSubtitle(selection)}
              </p>
            ) : null}
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
          {selection.kind === "bundle" ? (
            <BundleBody bundle={selection.bundle} />
          ) : null}
          {selection.kind === "entry" ? (
            <EntryBody
              entry={selection.entry}
              onSaved={onSaved}
              onCancel={onClose}
            />
          ) : null}
        </div>
      </div>
    </div>,
    document.body
  );
}

function BundleBody({ bundle }: { bundle: BundleNode }) {
  return (
    <>
      <p className="m-0 text-[13px] leading-relaxed text-base-content/70 [&_code]:rounded [&_code]:bg-base-200 [&_code]:px-1.5 [&_code]:py-px [&_code]:font-mono [&_code]:text-xs">
        При вызове <code>get_instruction_bundle(mode="{bundle.mode}")</code>{" "}
        агент получает следующие инструкции в этом порядке:
      </p>
      <ol className="m-0 flex list-decimal flex-col gap-1 pl-5 text-[13px] [&_code]:rounded [&_code]:bg-base-200 [&_code]:px-1.5 [&_code]:py-px [&_code]:font-mono [&_code]:text-xs">
        {bundle.includes.map((entry, index) => (
          <li key={`${entry.scope}:${entry.kind}:${String(index)}`}>
            <code>{entry.scope}</code>
            <span> · </span>
            <code>{entry.kind}</code>
            <span> · </span>
            <span className="text-base-content/70">
              {entry.editable ? "user (editable)" : "system (read-only)"}
            </span>
            {!entry.present ? (
              <span className="text-warning"> — не создана</span>
            ) : null}
          </li>
        ))}
      </ol>
    </>
  );
}

interface EntryBodyProps {
  entry: BundleEntryNode;
  onSaved: () => void;
  onCancel: () => void;
}

function EntryBody({ entry, onSaved, onCancel }: EntryBodyProps) {
  const [editing, setEditing] = useState(false);

  if (!entry.editable) {
    return (
      <ReadOnlySection
        title={`v${String(entry.current_version)}`}
        body={entry.text}
        hint="System-инструкция. Меняется только через манифест."
        monospace
      />
    );
  }

  if (!entry.present) {
    return (
      <p className="m-0 text-[13px] leading-relaxed text-base-content/70">
        У этого user-kind ещё нет записи в Mongo. Создание user-инструкций из UI
        пока не поддерживается — они появляются автоматически при инициализации.
      </p>
    );
  }

  const detail: InstructionDetail = {
    id: entry.instruction_id ?? "",
    kind: entry.kind,
    current_version: entry.current_version,
    text: entry.text,
    created_at: new Date(0).toISOString(),
    updated_at: new Date(0).toISOString()
  };

  if (editing) {
    return (
      <ReplaceInstructionTextForm
        instruction={detail}
        onSaved={() => {
          setEditing(false);
          onSaved();
        }}
        onCancel={() => {
          setEditing(false);
        }}
      />
    );
  }

  return (
    <>
      <pre className="m-0 whitespace-pre-wrap break-words rounded-md border border-base-300 bg-base-200 px-4 py-3.5 font-mono text-xs leading-relaxed">
        {entry.text}
      </pre>
      <div className="flex justify-end gap-2">
        <Button onClick={onCancel}>Закрыть</Button>
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
  );
}

function ReadOnlySection({
  title,
  body,
  hint,
  monospace
}: {
  title: string;
  body: string;
  hint?: string;
  monospace?: boolean;
}) {
  const proseClass =
    "m-0 whitespace-pre-wrap break-words rounded-md border border-base-300 bg-base-200 px-4 py-3.5 text-sm leading-relaxed";
  const monoClass =
    "m-0 whitespace-pre-wrap break-words rounded-md border border-base-300 bg-base-200 px-4 py-3.5 font-mono text-xs leading-relaxed";
  return (
    <section className="flex flex-col gap-1.5">
      <h3 className="m-0 text-[13px] font-bold uppercase tracking-wider text-base-content/70">
        {title}
      </h3>
      {hint ? <p className="m-0 text-xs text-base-content/60">{hint}</p> : null}
      <pre className={monospace ? monoClass : proseClass}>{body}</pre>
    </section>
  );
}

function selectionEyebrow(selection: SelectedNode) {
  if (selection.kind === "bundle") return "Bundle";
  return "Instruction";
}

function selectionTitle(selection: SelectedNode) {
  if (selection.kind === "bundle") return `mode: ${selection.bundle.mode}`;
  return `${selection.entry.scope} · ${selection.entry.kind}`;
}

function selectionSubtitle(selection: SelectedNode) {
  if (selection.kind === "bundle") {
    return `Состав bundle для mode "${selection.bundle.mode}"`;
  }
  return `Используется в bundle mode "${selection.bundle.mode}"`;
}
