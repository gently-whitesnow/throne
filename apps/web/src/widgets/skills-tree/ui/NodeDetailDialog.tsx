import { X } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { createPortal } from "react-dom";

import type { InstructionDetail } from "@/entities/instruction";
import { ReplaceInstructionTextForm } from "@/features/replace-instruction-text";
import { Button } from "@/shared/ui";

import type { BundleEntryNode, SelectedNode, SkillNode } from "../model/types";

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
      className="skills-tree-modal"
      role="presentation"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="skills-tree-modal__dialog"
      >
        <header className="skills-tree-modal__header">
          <div>
            <p className="skills-tree-modal__eyebrow">
              {selectionEyebrow(selection)}
            </p>
            <h2 id={titleId} className="skills-tree-modal__title">
              {selectionTitle(selection)}
            </h2>
            {selectionSubtitle(selection) ? (
              <p className="skills-tree-modal__subtitle">
                {selectionSubtitle(selection)}
              </p>
            ) : null}
          </div>
          <button
            type="button"
            className="skills-tree-modal__close"
            onClick={onClose}
            aria-label="Закрыть"
          >
            <X aria-hidden size={16} strokeWidth={2} />
          </button>
        </header>

        <div className="skills-tree-modal__body">
          {selection.kind === "skill" ? (
            <SkillBody skill={selection.skill} />
          ) : null}
          {selection.kind === "bundle" ? (
            <BundleBody skill={selection.skill} />
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

function SkillBody({ skill }: { skill: SkillNode }) {
  return (
    <>
      <ReadOnlySection
        title="Описание"
        body={skill.description}
        hint="Совпадает с frontmatter SKILL.md."
      />
      <ReadOnlySection
        title="Тело launcher-а (read-only)"
        body={skill.launcher_body}
        hint="Это содержимое попадает в файл .claude/skills и .agents/skills как тонкий launcher."
        monospace
      />
    </>
  );
}

function BundleBody({ skill }: { skill: SkillNode }) {
  return (
    <>
      <p className="skills-tree-modal__paragraph">
        При вызове <code>/{skill.name}</code> агент дёргает{" "}
        <code>get_instruction_bundle(mode="{skill.bundle.mode}")</code> и
        получает следующие инструкции в этом порядке:
      </p>
      <ol className="skills-tree-modal__include-list">
        {skill.bundle.includes.map((entry, index) => (
          <li key={`${entry.scope}:${entry.kind}:${String(index)}`}>
            <code>{entry.scope}</code>
            <span> · </span>
            <code>{entry.kind}</code>
            <span> · </span>
            <span className="skills-tree-modal__include-status">
              {entry.editable ? "user (editable)" : "system (read-only)"}
            </span>
            {!entry.present ? (
              <span className="skills-tree-modal__include-warn">
                {" "}
                — не создана
              </span>
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
      <p className="skills-tree-modal__paragraph">
        У этого user-kind ещё нет записи в Mongo. Создание user-инструкций из UI
        пока не поддерживается — это runtime-данные (см. ADR-0007), они
        появляются автоматически при инициализации.
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
      <pre className="skills-tree-modal__text">{entry.text}</pre>
      <div className="skills-tree-modal__actions">
        <Button
          variant="primary"
          onClick={() => {
            setEditing(true);
          }}
        >
          Редактировать
        </Button>
        <Button onClick={onCancel}>Закрыть</Button>
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
  return (
    <section className="skills-tree-modal__section">
      <h3 className="skills-tree-modal__section-title">{title}</h3>
      {hint ? <p className="skills-tree-modal__section-hint">{hint}</p> : null}
      <pre
        className={
          monospace
            ? "skills-tree-modal__text"
            : "skills-tree-modal__text skills-tree-modal__text--prose"
        }
      >
        {body}
      </pre>
    </section>
  );
}

function selectionEyebrow(selection: SelectedNode) {
  if (selection.kind === "skill") return "Skill";
  if (selection.kind === "bundle") return "Bundle";
  return "Instruction";
}

function selectionTitle(selection: SelectedNode) {
  if (selection.kind === "skill") return `/${selection.skill.name}`;
  if (selection.kind === "bundle")
    return `mode: ${selection.skill.bundle.mode}`;
  return `${selection.entry.scope} · ${selection.entry.kind}`;
}

function selectionSubtitle(selection: SelectedNode) {
  if (selection.kind === "skill") return selection.skill.description;
  if (selection.kind === "bundle") {
    return `Состав bundle для скилла /${selection.skill.name}`;
  }
  return `Используется в bundle mode "${selection.skill.bundle.mode}"`;
}
