import { useState } from "react";

import type { PromptPartPatch } from "@/entities/prompt-part-patch";

import { usePromptPartPatches } from "../model/use-prompt-part-patches";
import { usePatchDetail } from "../model/use-patch-detail";

import { PatchDetailPanel } from "./PatchDetailPanel";
import { PatchStatusBadge } from "./PatchStatusBadge";

/**
 * Patches tab: left rail with PromptPartPatch rows (newest first), right pane
 * with detail + apply / apply-with-edit / reject actions. Owner-scoped via the
 * server; realtime fanout drives the refresh.
 */
export function PatchesTab() {
  const { state, reload } = usePromptPartPatches();
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
  if (items.length === 0) {
    return (
      <p className="m-0 rounded border border-base-300 bg-base-100 p-4 text-sm text-base-content/60">
        Патчи появятся, когда фронтир-агент через MCP вызовет
        propose_prompt_part_patch. Запусти dream-проход в подключённом агенте
        либо подожди cron.
      </p>
    );
  }

  return (
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
                <span className="ml-auto text-[10px] text-base-content/60">
                  {p.evidence_card_ids.length} ev.
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
