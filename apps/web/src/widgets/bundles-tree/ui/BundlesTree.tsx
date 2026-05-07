import {
  ChevronDown,
  ChevronRight,
  FileText,
  Layers,
  Lock,
  Pencil
} from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";

import { instructionKindLabel } from "@/entities/instruction";
import { HttpError, httpGet, instructionsEndpoints } from "@/shared/api";

import type {
  BundleEntryNode,
  BundleNode,
  BundlesTreeData,
  SelectedNode
} from "../model/types";
import { NodeDetailDialog } from "./NodeDetailDialog";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; tree: BundlesTreeData }
  | { kind: "error"; message: string };

export function BundlesTree() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [reloadKey, setReloadKey] = useState(0);
  const [selected, setSelected] = useState<SelectedNode | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: "loading" });
    httpGet<BundlesTreeData>(
      instructionsEndpoints.getBundlesTree(),
      controller.signal
    )
      .then((tree) => {
        setState({ kind: "ready", tree });
        setExpanded((prev) => {
          const next = { ...prev };
          for (const bundle of tree.bundles) {
            next[bundleKey(bundle)] = next[bundleKey(bundle)] ?? true;
          }
          return next;
        });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить дерево бандлов (${String(err.status)}).`
            : "Не удалось загрузить дерево бандлов.";
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

  const bundles = useMemo(
    () => (state.kind === "ready" ? state.tree.bundles : []),
    [state]
  );

  return (
    <section
      className="mx-auto flex max-w-5xl flex-col gap-4"
      aria-label="Дерево бандлов"
    >
      <header className="flex flex-col gap-1.5">
        <h2 className="m-0 text-xl font-bold tracking-tight">Bundles</h2>
        <p className="m-0 text-sm leading-relaxed text-base-content/70">
          Точное содержимое, которое попадает агенту при{" "}
          <code className="rounded bg-base-200 px-1.5 py-px font-mono text-xs">
            get_instruction_bundle(mode)
          </code>
          . Источник правды —{" "}
          <code className="rounded bg-base-200 px-1.5 py-px font-mono text-xs">
            specs/manifest/throne-skills.yaml
          </code>
          .
        </p>
      </header>

      <div className="rounded-lg border border-base-300 bg-base-100 p-2">
        {state.kind === "loading" && (
          <p className="m-0 p-4 text-[13px] text-base-content/60">Загрузка…</p>
        )}
        {state.kind === "error" && (
          <p role="alert" className="m-0 p-4 text-[13px] text-base-content/60">
            {state.message}
          </p>
        )}
        {state.kind === "ready" && bundles.length === 0 && (
          <p className="m-0 p-4 text-[13px] text-base-content/60">
            В манифесте нет ни одного бандла.
          </p>
        )}
        {state.kind === "ready" && bundles.length > 0 && (
          <ul className="m-0 list-none p-0" role="tree">
            {bundles.map((bundle) => (
              <BundleRow
                key={bundle.mode}
                bundle={bundle}
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

interface BundleRowProps {
  bundle: BundleNode;
  expanded: Record<string, boolean>;
  onToggle: (key: string) => void;
  onOpen: (node: SelectedNode) => void;
}

function BundleRow({ bundle, expanded, onToggle, onOpen }: BundleRowProps) {
  const open = expanded[bundleKey(bundle)] ?? true;

  return (
    <li className="mt-1 first:mt-0" role="treeitem" aria-expanded={open}>
      <NodeRow
        expandable
        expanded={open}
        onExpand={() => {
          onToggle(bundleKey(bundle));
        }}
        icon={<Layers aria-hidden size={16} strokeWidth={2} />}
        label={`mode: ${bundle.mode}`}
        meta={`${String(bundle.includes.length)} инструкций`}
        onOpen={() => {
          onOpen({ kind: "bundle", bundle });
        }}
      />
      {open ? (
        <ul
          className="m-0 ml-4 list-none border-l border-dashed border-base-300 p-0"
          role="group"
        >
          {bundle.includes.map((entry, index) => (
            <EntryRow
              key={`${entry.scope}:${entry.kind}:${String(index)}`}
              entry={entry}
              onOpen={() => {
                onOpen({ kind: "entry", bundle, entry });
              }}
            />
          ))}
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
        icon={<FileText aria-hidden size={14} strokeWidth={2} />}
        label={
          <span className="inline-flex items-center gap-2 font-semibold">
            <span
              className="inline-flex h-[18px] items-center rounded-full px-2 text-[10px] font-bold uppercase tracking-wide"
              style={{ background: meta.surface, color: meta.ink }}
            >
              {entry.scope}
            </span>
            <span className="font-mono text-[13px] text-base-content">
              {entry.kind}
            </span>
          </span>
        }
        meta={
          <span className="inline-flex items-center gap-1.5">
            {scopeIcon}
            <span>
              {entry.editable ? "user / редактируется" : "system / read-only"}
            </span>
            {status ? <span className="text-warning">— {status}</span> : null}
          </span>
        }
        onOpen={onOpen}
      />
    </li>
  );
}

interface NodeRowProps {
  icon: React.ReactNode;
  label: React.ReactNode;
  meta?: React.ReactNode;
  expandable?: boolean;
  expanded?: boolean;
  onExpand?: () => void;
  onOpen: () => void;
}

function NodeRow({
  icon,
  label,
  meta,
  expandable = false,
  expanded = false,
  onExpand,
  onOpen
}: NodeRowProps) {
  return (
    <div className="flex items-center gap-1 rounded-md p-1 hover:bg-base-200">
      {expandable ? (
        <button
          type="button"
          className="inline-flex h-[22px] w-[22px] flex-shrink-0 items-center justify-center rounded text-base-content/60 hover:bg-base-300 hover:text-base-content"
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
        <span className="inline-block h-[22px] w-[22px] flex-shrink-0" />
      )}
      <button
        type="button"
        className="flex flex-1 items-center gap-2.5 rounded-md px-2 py-1 text-left text-base-content focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-2"
        onClick={onOpen}
      >
        <span className="inline-flex flex-shrink-0 items-center text-base-content/60">
          {icon}
        </span>
        <span className="text-sm font-semibold text-base-content">{label}</span>
        {meta ? (
          <span className="ml-auto inline-flex items-center gap-1.5 text-right text-xs font-normal text-base-content/60">
            {meta}
          </span>
        ) : null}
      </button>
    </div>
  );
}

function bundleKey(bundle: BundleNode) {
  return `bundle:${bundle.mode}`;
}
