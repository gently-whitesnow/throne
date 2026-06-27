import { useState } from "react";

import {
  useListPromptParts,
  type PromptPartListItem
} from "@/entities/prompt-part";
import { errorMessage } from "@/shared/lib";

import { PartsList } from "./PartsList";
import {
  PromptPartDetailDialog,
  type PromptPartDialogTarget
} from "./PromptPartDetailDialog";

interface PromptPartsBoardProps {
  /** key `${scope}/${key}` → proposed-patch count, for the «N правок» badge. */
  patchCounts?: Map<string, number>;
  /** Surface a part's proposed improvements next to the part. */
  onShowPatches?: (part: PromptPartListItem) => void;
}

/**
 * Каталог prompt_parts (system read-only + user editable) с инлайновыми ролями
 * по режимам. Без собственного page-заголовка — встраивается в слот «System-
 * промпт / части», который задаёт заголовок и подпись источника.
 */
export function PromptPartsBoard({
  patchCounts,
  onShowPatches
}: PromptPartsBoardProps) {
  const partsQuery = useListPromptParts();

  const [partDialog, setPartDialog] = useState<PromptPartDialogTarget | null>(
    null
  );

  const parts: PromptPartListItem[] = partsQuery.data ?? [];

  const error = partsQuery.error
    ? errorMessage(partsQuery.error, { base: "Не удалось загрузить данные" })
    : null;
  const loading = partsQuery.isPending;

  return (
    <div className="flex flex-col gap-4" aria-label="Части промпта">
      {error ? (
        <p role="alert" className="m-0 text-[13px] text-base-content/60">
          {error}
        </p>
      ) : null}
      {loading ? (
        <p className="m-0 text-[13px] text-base-content/60">Загрузка…</p>
      ) : null}

      {!loading && !error ? (
        <PartsList
          parts={parts}
          patchCounts={patchCounts}
          onOpenPart={(part) => {
            setPartDialog({ mode: "detail", part });
          }}
          onShowPatches={onShowPatches}
          onCreatePart={() => {
            setPartDialog({ mode: "create" });
          }}
        />
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
