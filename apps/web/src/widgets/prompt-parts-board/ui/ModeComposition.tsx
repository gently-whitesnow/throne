import { EyeOff, Plus } from "lucide-react";
import { useMemo, useState } from "react";

import {
  PROMPT_PART_MODE_LABELS,
  type PromptPartListItem,
  type PromptPartMode
} from "@/entities/prompt-part";
import { Button, CollapsibleSection } from "@/shared/ui";

import { bucketize } from "../model/block-buckets";
import { BlockRow } from "./BlockRow";

interface ModeCompositionProps {
  parts: PromptPartListItem[];
  mode: PromptPartMode;
  patchCounts?: Map<string, number>;
  onOpenPart: (part: PromptPartListItem) => void;
  onShowPatches?: (part: PromptPartListItem) => void;
  onCreatePart: () => void;
}

/**
 * Composition of the system prompt for the selected mode: «входит» ships,
 * «доступно, выкл» can be switched on, «не входит» is collapsed out of the way.
 */
export function ModeComposition({
  parts,
  mode,
  patchCounts,
  onOpenPart,
  onShowPatches,
  onCreatePart
}: ModeCompositionProps) {
  const [roleError, setRoleError] = useState<string | null>(null);
  const orderOf = useMemo(
    () => new Map(parts.map((p, i) => [p.id, i])),
    [parts]
  );
  const buckets = useMemo(() => bucketize(parts, mode), [parts, mode]);
  const modeLabel = PROMPT_PART_MODE_LABELS[mode];

  const renderRow = (part: PromptPartListItem) => (
    <BlockRow
      key={part.id}
      part={part}
      mode={mode}
      orderForNew={orderOf.get(part.id) ?? 0}
      patchCount={patchCounts?.get(`${part.scope}/${part.key}`) ?? 0}
      onOpen={() => {
        onOpenPart(part);
      }}
      onShowPatches={
        onShowPatches
          ? () => {
              onShowPatches(part);
            }
          : undefined
      }
      onRoleError={setRoleError}
    />
  );

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div className="flex items-start justify-between gap-3">
        <p className="m-0 max-w-[60ch] text-xs leading-relaxed text-base-content/60">
          Системный промпт режима <b>«{modeLabel}»</b> = блоки из «входит».
          Доступные можно включить на запуск, остальные в этом режиме не
          используются.
        </p>
        <Button
          variant="primary"
          icon={<Plus aria-hidden size={14} strokeWidth={2.5} />}
          onClick={onCreatePart}
        >
          Создать блок
        </Button>
      </div>

      {roleError ? (
        <p role="alert" className="m-0 text-sm text-error">
          {roleError}
        </p>
      ) : null}

      <Bucket
        title={`Входит в «${modeLabel}»`}
        count={buckets.included.length}
        empty="Ни один блок не уходит в этом режиме."
      >
        {buckets.included.map(renderRow)}
      </Bucket>

      <Bucket
        title="Доступно, но выключено"
        count={buckets.available.length}
        empty="Нет доступных выключенных блоков."
      >
        {buckets.available.map(renderRow)}
      </Bucket>

      {buckets.excluded.length > 0 ? (
        <CollapsibleSection
          icon={<EyeOff aria-hidden size={14} strokeWidth={2} />}
          title="Не входит в этот режим"
          count={buckets.excluded.length}
        >
          <ul className="m-0 mt-2 flex list-none flex-col gap-1.5 p-0">
            {buckets.excluded.map(renderRow)}
          </ul>
        </CollapsibleSection>
      ) : null}
    </div>
  );
}

function Bucket({
  title,
  count,
  empty,
  children
}: {
  title: string;
  count: number;
  empty: string;
  children: React.ReactNode;
}) {
  return (
    <section className="flex flex-col gap-2">
      <h3 className="m-0 flex items-center gap-2 text-[13px] font-bold uppercase tracking-wider text-base-content/70">
        {title}
        <span className="rounded-full bg-base-200 px-1.5 py-0.5 text-[11px] font-semibold tabular-nums text-base-content/60">
          {count}
        </span>
      </h3>
      {count === 0 ? (
        <p className="m-0 text-[13px] text-base-content/50">{empty}</p>
      ) : (
        <ul className="m-0 flex list-none flex-col gap-1.5 p-0">{children}</ul>
      )}
    </section>
  );
}
