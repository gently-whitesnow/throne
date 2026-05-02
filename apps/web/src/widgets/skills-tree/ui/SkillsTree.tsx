import {
  ChevronDown,
  ChevronRight,
  FileText,
  Layers,
  Lock,
  Pencil,
  ScrollText
} from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";

import { instructionKindLabel } from "@/entities/instruction";
import { HttpError, httpGet, instructionsEndpoints } from "@/shared/api";

import type {
  BundleEntryNode,
  SelectedNode,
  SkillNode,
  SkillsTreeData
} from "../model/types";
import { NodeDetailDialog } from "./NodeDetailDialog";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; tree: SkillsTreeData }
  | { kind: "error"; message: string };

export function SkillsTree() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [reloadKey, setReloadKey] = useState(0);
  const [selected, setSelected] = useState<SelectedNode | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: "loading" });
    httpGet<SkillsTreeData>(
      instructionsEndpoints.getSkillsTree(),
      controller.signal
    )
      .then((tree) => {
        setState({ kind: "ready", tree });
        setExpanded((prev) => {
          const next = { ...prev };
          for (const skill of tree.skills) {
            next[skillKey(skill)] = next[skillKey(skill)] ?? true;
            next[bundleKey(skill)] = next[bundleKey(skill)] ?? true;
          }
          return next;
        });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить дерево скиллов (${String(err.status)}).`
            : "Не удалось загрузить дерево скиллов.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey]);

  const toggle = useCallback((key: string) => {
    setExpanded((prev) => ({ ...prev, [key]: !prev[key] }));
  }, []);

  const handleSaved = useCallback(() => {
    setSelected(null);
    setReloadKey((k) => k + 1);
  }, []);

  const skills = useMemo(
    () => (state.kind === "ready" ? state.tree.skills : []),
    [state]
  );

  return (
    <section className="skills-tree" aria-label="Дерево скиллов">
      <header className="skills-tree__header">
        <h2 className="skills-tree__title">Skills</h2>
        <p className="skills-tree__hint">
          Точное содержимое, которое попадает агенту при вызове команды.
          Источник правды — <code>specs/manifest/throne-skills.yaml</code>.
        </p>
      </header>

      <div className="skills-tree__body">
        {state.kind === "loading" && (
          <p className="skills-tree__placeholder">Загрузка…</p>
        )}
        {state.kind === "error" && (
          <p role="alert" className="skills-tree__placeholder">
            {state.message}
          </p>
        )}
        {state.kind === "ready" && skills.length === 0 && (
          <p className="skills-tree__placeholder">
            В манифесте нет ни одного скилла.
          </p>
        )}
        {state.kind === "ready" && skills.length > 0 && (
          <ul className="skills-tree__list" role="tree">
            {skills.map((skill) => (
              <SkillRow
                key={skill.name}
                skill={skill}
                expanded={expanded}
                onToggle={toggle}
                onOpen={(node) => {
                  setSelected(node);
                }}
              />
            ))}
          </ul>
        )}
      </div>

      {selected ? (
        <NodeDetailDialog
          selection={selected}
          onClose={() => {
            setSelected(null);
          }}
          onSaved={handleSaved}
        />
      ) : null}
    </section>
  );
}

interface SkillRowProps {
  skill: SkillNode;
  expanded: Record<string, boolean>;
  onToggle: (key: string) => void;
  onOpen: (node: SelectedNode) => void;
}

function SkillRow({ skill, expanded, onToggle, onOpen }: SkillRowProps) {
  const skillOpen = expanded[skillKey(skill)] ?? true;
  const bundleOpen = expanded[bundleKey(skill)] ?? true;

  return (
    <li
      className="skills-tree__skill"
      role="treeitem"
      aria-expanded={skillOpen}
    >
      <NodeRow
        depth={0}
        expandable
        expanded={skillOpen}
        onExpand={() => {
          onToggle(skillKey(skill));
        }}
        icon={<ScrollText aria-hidden size={16} strokeWidth={2} />}
        label={`/${skill.name}`}
        meta={skill.description}
        onOpen={() => {
          onOpen({ kind: "skill", skill });
        }}
      />
      {skillOpen ? (
        <ul className="skills-tree__children" role="group">
          <li role="treeitem" aria-expanded={bundleOpen}>
            <NodeRow
              depth={1}
              expandable
              expanded={bundleOpen}
              onExpand={() => {
                onToggle(bundleKey(skill));
              }}
              icon={<Layers aria-hidden size={15} strokeWidth={2} />}
              label={`bundle: ${skill.bundle.mode}`}
              meta={`${String(skill.bundle.includes.length)} инструкций`}
              onOpen={() => {
                onOpen({ kind: "bundle", skill });
              }}
            />
            {bundleOpen ? (
              <ul className="skills-tree__children" role="group">
                {skill.bundle.includes.map((entry, index) => (
                  <EntryRow
                    key={`${entry.scope}:${entry.kind}:${String(index)}`}
                    entry={entry}
                    onOpen={() => {
                      onOpen({ kind: "entry", skill, entry });
                    }}
                  />
                ))}
              </ul>
            ) : null}
          </li>
        </ul>
      ) : null}
    </li>
  );
}

interface EntryRowProps {
  entry: BundleEntryNode;
  onOpen: () => void;
}

function EntryRow({ entry, onOpen }: EntryRowProps) {
  const meta = instructionKindLabel(entry.kind);
  const scopeIcon = entry.editable ? (
    <Pencil aria-hidden size={14} strokeWidth={2} />
  ) : (
    <Lock aria-hidden size={14} strokeWidth={2} />
  );
  const status = entry.present ? null : "не создана";

  return (
    <li role="treeitem">
      <NodeRow
        depth={2}
        icon={<FileText aria-hidden size={14} strokeWidth={2} />}
        label={
          <span className="skills-tree__entry-label">
            <span
              className="skills-tree__badge"
              style={{ background: meta.surface, color: meta.ink }}
            >
              {entry.scope}
            </span>
            <span className="skills-tree__entry-kind">{entry.kind}</span>
          </span>
        }
        meta={
          <span className="skills-tree__entry-meta">
            {scopeIcon}
            <span>
              {entry.editable ? "user / редактируется" : "system / read-only"}
            </span>
            {status ? (
              <span className="skills-tree__entry-warn">— {status}</span>
            ) : null}
          </span>
        }
        onOpen={onOpen}
      />
    </li>
  );
}

interface NodeRowProps {
  depth: 0 | 1 | 2;
  icon: React.ReactNode;
  label: React.ReactNode;
  meta?: React.ReactNode;
  expandable?: boolean;
  expanded?: boolean;
  onExpand?: () => void;
  onOpen: () => void;
}

function NodeRow({
  depth,
  icon,
  label,
  meta,
  expandable = false,
  expanded = false,
  onExpand,
  onOpen
}: NodeRowProps) {
  return (
    <div
      className={`skills-tree__row skills-tree__row--depth-${String(depth)}`}
    >
      {expandable ? (
        <button
          type="button"
          className="skills-tree__chevron"
          onClick={onExpand}
          aria-label={expanded ? "Свернуть" : "Развернуть"}
        >
          {expanded ? (
            <ChevronDown aria-hidden size={14} strokeWidth={2.5} />
          ) : (
            <ChevronRight aria-hidden size={14} strokeWidth={2.5} />
          )}
        </button>
      ) : (
        <span className="skills-tree__chevron skills-tree__chevron--placeholder" />
      )}
      <button
        type="button"
        className="skills-tree__row-button"
        onClick={onOpen}
      >
        <span className="skills-tree__row-icon">{icon}</span>
        <span className="skills-tree__row-label">{label}</span>
        {meta ? <span className="skills-tree__row-meta">{meta}</span> : null}
      </button>
    </div>
  );
}

function skillKey(skill: SkillNode) {
  return `skill:${skill.name}`;
}

function bundleKey(skill: SkillNode) {
  return `bundle:${skill.name}`;
}
