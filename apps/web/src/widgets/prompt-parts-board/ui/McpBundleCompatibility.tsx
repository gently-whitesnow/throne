import { FileText, Layers, Lock, Pencil } from "lucide-react";

import type {
  BundleEntryNode,
  BundlesTreeData
} from "../model/use-bundles-tree";

interface McpBundleCompatibilityProps {
  bundlesTree: BundlesTreeData | undefined;
}

/**
 * Read-only overview of the MCP `get_prompt_bundle(mode)` payload for all
 * modes (including dream/fix). Source of truth is the skill manifest; this is
 * exactly what an external agent receives over MCP.
 */
export function McpBundleCompatibility({
  bundlesTree
}: McpBundleCompatibilityProps) {
  const bundles = bundlesTree?.bundles ?? [];

  if (bundles.length === 0) {
    return (
      <p className="m-0 text-[13px] text-base-content/60">
        В манифесте нет ни одного бандла.
      </p>
    );
  }

  return (
    <ul className="m-0 flex list-none flex-col gap-3 p-0">
      {bundles.map((bundle) => (
        <li
          key={bundle.mode}
          className="rounded-md border border-base-300 bg-base-100 p-3"
        >
          <div className="flex items-center gap-2">
            <Layers
              aria-hidden
              size={16}
              strokeWidth={2}
              className="text-base-content/60"
            />
            <span className="font-mono text-[13px] font-semibold text-base-content">
              mode: {bundle.mode}
            </span>
            <span className="ml-auto text-xs text-base-content/50">
              {bundle.includes.length} инструкций
            </span>
          </div>
          <ul className="m-0 mt-2 flex list-none flex-col gap-1 border-l border-dashed border-base-300 p-0 pl-3">
            {bundle.includes.map((entry, index) => (
              <EntryLine
                key={`${entry.scope}:${entry.key}:${String(index)}`}
                entry={entry}
              />
            ))}
          </ul>
        </li>
      ))}
    </ul>
  );
}

function EntryLine({ entry }: { entry: BundleEntryNode }) {
  return (
    <li className="flex items-center gap-2 text-xs">
      <FileText
        aria-hidden
        size={13}
        strokeWidth={2}
        className="text-base-content/40"
      />
      <span className="font-mono text-base-content/40">{entry.scope}</span>
      <span className="font-mono font-semibold text-base-content">
        {entry.key}
      </span>
      <span className="ml-auto inline-flex items-center gap-1 text-base-content/60">
        {entry.editable ? (
          <Pencil aria-hidden size={12} strokeWidth={2} />
        ) : (
          <Lock aria-hidden size={12} strokeWidth={2} />
        )}
        {entry.editable ? "user" : "system"}
      </span>
      {!entry.present ? <span className="text-warning">не создана</span> : null}
    </li>
  );
}
