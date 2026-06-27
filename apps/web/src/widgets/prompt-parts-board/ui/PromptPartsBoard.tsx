import { useState } from "react";

import {
  useListPromptParts,
  type PromptPartListItem,
  type PromptPartMode
} from "@/entities/prompt-part";
import { errorMessage } from "@/shared/lib";

import { ModeComposition } from "./ModeComposition";
import { ModeRail } from "./ModeRail";
import {
  PromptPartDetailDialog,
  type PromptPartDialogTarget
} from "./PromptPartDetailDialog";

interface PromptPartsBoardProps {
  /** key `${scope}/${key}` → proposed-patch count, for the «N правок» badge. */
  patchCounts?: Map<string, number>;
  /** Surface a block's proposed improvements next to the block. */
  onShowPatches?: (part: PromptPartListItem) => void;
}

/**
 * Каталог блоков системного промпта в раскладке «режим → состав»: слева режимы
 * со счётчиком, справа — что входит / доступно / не входит в выбранном режиме.
 * Без собственного page-заголовка — встраивается в слот «System-промпт».
 */
export function PromptPartsBoard({
  patchCounts,
  onShowPatches
}: PromptPartsBoardProps) {
  const partsQuery = useListPromptParts();
  const [mode, setMode] = useState<PromptPartMode>("work");
  const [partDialog, setPartDialog] = useState<PromptPartDialogTarget | null>(
    null
  );

  const parts: PromptPartListItem[] = partsQuery.data ?? [];

  const error = partsQuery.error
    ? errorMessage(partsQuery.error, { base: "Не удалось загрузить данные" })
    : null;
  const loading = partsQuery.isPending;

  return (
    <div className="flex flex-col gap-4" aria-label="Блоки системного промпта">
      {error ? (
        <p role="alert" className="m-0 text-[13px] text-base-content/60">
          {error}
        </p>
      ) : null}
      {loading ? (
        <p className="m-0 text-[13px] text-base-content/60">Загрузка…</p>
      ) : null}

      {!loading && !error ? (
        <div className="grid gap-4 md:grid-cols-[170px_1fr]">
          <ModeRail parts={parts} selected={mode} onSelect={setMode} />
          <ModeComposition
            parts={parts}
            mode={mode}
            patchCounts={patchCounts}
            onOpenPart={(part) => {
              setPartDialog({ mode: "detail", part });
            }}
            onShowPatches={onShowPatches}
            onCreatePart={() => {
              setPartDialog({ mode: "create" });
            }}
          />
        </div>
      ) : null}

      {partDialog ? (
        <PromptPartDetailDialog
          target={partDialog}
          onClose={() => {
            setPartDialog(null);
          }}
        />
      ) : null}
    </div>
  );
}
