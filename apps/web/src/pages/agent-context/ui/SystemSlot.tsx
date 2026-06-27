import { useState } from "react";

import type { PromptPartListItem } from "@/entities/prompt-part";
import { DreamsTab } from "@/widgets/improvements-dreams-tab";
import { PatchesTab } from "@/widgets/improvements-patches-tab";
import { PromptPartsBoard } from "@/widgets/prompt-parts-board";

type SubTab = "parts" | "improvements" | "history";

interface SystemSlotProps {
  /** key `${scope}/${key}` → proposed-patch count, for the «N правок» badge. */
  patchCounts: Map<string, number>;
  /** Total proposed patches, shown on the «Улучшения» sub-tab. */
  proposedTotal: number;
}

interface PatchTarget {
  scope: string;
  key: string;
}

/**
 * Slot 1 — the system-prompt material. The catalog of parts is primary; the two
 * companion views («Улучшения» / «История») fold Improvements into the same
 * model — how the parts evolve, shown right next to the parts they touch. From a
 * part you reach its proposed edits (the «N правок» badge jumps here, filtered).
 */
export function SystemSlot({ patchCounts, proposedTotal }: SystemSlotProps) {
  const [tab, setTab] = useState<SubTab>("parts");
  const [target, setTarget] = useState<PatchTarget | null>(null);

  const showPatches = (part: PromptPartListItem) => {
    setTarget({ scope: part.scope, key: part.key });
    setTab("improvements");
  };

  const selectTab = (next: SubTab) => {
    // A tab click is the «show everything» entry; the per-part filter is reached
    // only via a part's «N правок» badge, so reset it on any direct tab switch.
    setTarget(null);
    setTab(next);
  };

  return (
    <div className="flex flex-col gap-4">
      <div role="tablist" className="tabs tabs-boxed w-fit">
        <SubTabButton
          active={tab === "parts"}
          label="Части"
          onClick={() => {
            selectTab("parts");
          }}
        />
        <SubTabButton
          active={tab === "improvements"}
          label="Улучшения"
          count={proposedTotal}
          onClick={() => {
            selectTab("improvements");
          }}
        />
        <SubTabButton
          active={tab === "history"}
          label="История"
          onClick={() => {
            selectTab("history");
          }}
        />
      </div>

      <div role="tabpanel">
        {tab === "parts" ? (
          <PromptPartsBoard
            patchCounts={patchCounts}
            onShowPatches={showPatches}
          />
        ) : tab === "improvements" ? (
          <div className="flex flex-col gap-3">
            <p className="m-0 text-xs leading-relaxed text-base-content/60">
              Предложенные правки user-частей от dream-агента. Apply /
              apply-with-edit / reject — твоё решение.
            </p>
            <PatchesTab
              targetScope={target?.scope}
              targetKey={target?.key}
              onClearTarget={() => {
                setTarget(null);
              }}
            />
          </div>
        ) : (
          <div className="flex flex-col gap-3">
            <p className="m-0 text-xs leading-relaxed text-base-content/60">
              История проходов dream-агента: что разбирали и какие правки
              родились.
            </p>
            <DreamsTab />
          </div>
        )}
      </div>
    </div>
  );
}

function SubTabButton({
  active,
  label,
  count,
  onClick
}: {
  active: boolean;
  label: string;
  count?: number;
  onClick: () => void;
}) {
  return (
    <button
      role="tab"
      type="button"
      aria-selected={active}
      className={active ? "tab tab-active" : "tab"}
      onClick={onClick}
    >
      {label}
      {count && count > 0 ? (
        <span className="ml-1.5 inline-flex h-[16px] min-w-[16px] items-center justify-center rounded-full bg-warning px-1 text-[10px] font-bold leading-none tabular-nums text-warning-content">
          {count}
        </span>
      ) : null}
    </button>
  );
}
