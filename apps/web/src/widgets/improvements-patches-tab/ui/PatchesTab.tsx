import { X } from "lucide-react";
import { useState } from "react";

import type { PromptPartPatch } from "@/entities/prompt-part-patch";

import { usePromptPartPatches } from "../model/use-prompt-part-patches";
import { usePatchDetail } from "../model/use-patch-detail";

import { PatchDetailPanel } from "./PatchDetailPanel";
import { PatchStatusBadge } from "./PatchStatusBadge";

interface PatchesTabProps {
  /** When set, list only the patches targeting this part (scope/key). */
  targetScope?: string;
  targetKey?: string;
  /** Clear the part filter (back to all proposed improvements). */
  onClearTarget?: () => void;
}

/**
 * Patches tab: left rail with PromptPartPatch rows (newest first), right pane
 * with detail + apply / apply-with-edit / reject actions. Owner-scoped via the
 * server; realtime fanout drives the refresh. Optionally scoped to a single
 * part so a part's proposed improvements show next to the part itself.
 */
export function PatchesTab({
  targetScope,
  targetKey,
  onClearTarget
}: PatchesTabProps = {}) {
  const filtered = Boolean(targetScope && targetKey);
  const { state, reload } = usePromptPartPatches(
    filtered ? { targetScope, targetKey } : {}
  );
  const [selected, setSelected] = useState<string | null>(null);
  const detail = usePatchDetail(selected);

  if (state.kind === "loading") {
    return (
      <div className="p-4 text-sm text-base-content/60">Загрузка патчей...</div>
    );
  }
  if (state.kind === "error") {
    return (
      <p
        role="alert"
        className="rounded border border-error/30 bg-error/10 p-3 text-sm text-error"
      >
        {state.message}
      </p>
    );
  }

  const items = state.items;
  const filterChip =
    filtered && onClearTarget ? (
      <FilterChip
        label={`${targetScope ?? ""}/${targetKey ?? ""}`}
        onClear={onClearTarget}
      />
    ) : null;

  if (items.length === 0) {
    return (
      <div className="flex flex-col gap-3">
        {filterChip}
        <p className="m-0 rounded border border-base-300 bg-base-100 p-4 text-sm text-base-content/60">
          {filtered
            ? "Для этого блока пока нет предложенных правок."
            : "Здесь появятся правки от dream-прохода. Запусти его в подключённом агенте скиллом «/dream» — он разберёт твои диалоги и предложит улучшения блоков, а ты примешь их тут: apply / apply-with-edit / reject."}
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      {filterChip}
      <div className="grid gap-4 lg:grid-cols-[minmax(0,320px)_1fr]">
        <PatchList
          items={items}
          selectedId={selected}
          onSelect={(id) => {
            setSelected(id);
          }}
        />
        <div>
          {selected === null ? (
            <p className="m-0 rounded border border-dashed border-base-300 bg-base-100 p-4 text-sm text-base-content/60">
              Выбери патч слева, чтобы увидеть diff и принять решение.
            </p>
          ) : detail.state.kind === "loading" ? (
            <p className="m-0 text-sm text-base-content/60">
              Загрузка деталей...
            </p>
          ) : detail.state.kind === "error" ? (
            <p
              role="alert"
              className="rounded border border-error/30 bg-error/10 p-3 text-sm text-error"
            >
              {detail.state.message}
            </p>
          ) : detail.state.kind === "ready" ? (
            <PatchDetailPanel
              detail={detail.state.detail}
              onChanged={() => {
                reload();
                detail.reload();
              }}
            />
          ) : null}
        </div>
      </div>
    </div>
  );
}

function FilterChip({
  label,
  onClear
}: {
  label: string;
  onClear: () => void;
}) {
  return (
    <div className="flex items-center gap-2 text-xs text-base-content/70">
      <span>Правки части</span>
      <span className="badge badge-soft badge-warning font-mono">{label}</span>
      <button
        type="button"
        onClick={onClear}
        className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-base-content/60 transition-[color,background-color,scale] hover:bg-base-200 hover:text-base-content active:scale-[0.96]"
      >
        <X aria-hidden size={12} strokeWidth={2.5} />
        все правки
      </button>
    </div>
  );
}

function PatchList({
  items,
  selectedId,
  onSelect
}: {
  items: PromptPartPatch[];
  selectedId: string | null;
  onSelect: (id: string) => void;
}) {
  return (
    <ul
      className="m-0 flex max-h-[70vh] flex-col gap-1 overflow-auto rounded border border-base-300 bg-base-100 p-2"
      aria-label="PromptPartPatches"
    >
      {items.map((p) => {
        const isSelected = p.id === selectedId;
        const baseClass =
          "flex w-full flex-col gap-1 rounded px-3 py-2 text-left transition-colors";
        const cls = isSelected
          ? `${baseClass} bg-primary/10 text-primary`
          : `${baseClass} hover:bg-base-200`;
        return (
          <li key={p.id} className="m-0 list-none">
            <button
              type="button"
              className={cls}
              onClick={() => {
                onSelect(p.id);
              }}
            >
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold uppercase tracking-wide">
                  {`${p.target_scope}/${p.target_key}`}
                </span>
                <PatchStatusBadge status={p.status} />
                <span className="badge badge-soft badge-neutral text-[10px]">
                  {p.operation}
                </span>
              </div>
              <p className="m-0 line-clamp-2 text-xs text-base-content/70">
                {firstLine(p.rationale) || firstLine(p.patch_text) || p.id}
              </p>
              <span className="text-[10px] text-base-content/50">
                {new Date(p.created_at).toLocaleString()}
              </span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}

function firstLine(text: string): string {
  const trimmed = text.trim();
  const idx = trimmed.indexOf("\n");
  return idx === -1 ? trimmed : trimmed.slice(0, idx);
}
