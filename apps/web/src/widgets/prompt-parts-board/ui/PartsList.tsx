import { Plus } from "lucide-react";
import { useState } from "react";

import type { PromptPartListItem } from "@/entities/prompt-part";
import { Button } from "@/shared/ui";

import { PartRow } from "./PartRow";

interface PartsListProps {
  parts: PromptPartListItem[];
  /** key `${scope}/${key}` → proposed-patch count, for the «N правок» badge. */
  patchCounts?: Map<string, number>;
  onOpenPart: (part: PromptPartListItem) => void;
  onShowPatches?: (part: PromptPartListItem) => void;
  onCreatePart: () => void;
}

/**
 * Single prompt-parts list grouped by scope. System parts are manifest-managed
 * (read-only text and roles); user parts get full CRUD plus inline per-mode role
 * controls. A part's per-mode role drives the embedded composition for that mode.
 */
export function PartsList({
  parts,
  patchCounts,
  onOpenPart,
  onShowPatches,
  onCreatePart
}: PartsListProps) {
  const [roleError, setRoleError] = useState<string | null>(null);

  const systemParts = parts.filter((p) => p.scope === "system");
  const userParts = parts.filter((p) => p.scope === "user");

  return (
    <div className="flex flex-col gap-4">
      {roleError ? (
        <p role="alert" className="m-0 text-sm text-error">
          {roleError}
        </p>
      ) : null}

      <ScopeGroup
        title="System"
        hint="Засеяны из манифеста и реконсайлятся на старте. Текст и роли read-only."
        empty="Нет system-частей."
        parts={systemParts}
        readOnly
        patchCounts={patchCounts}
        onOpenPart={onOpenPart}
        onShowPatches={onShowPatches}
        onRoleError={setRoleError}
      />

      <ScopeGroup
        title="User"
        hint="Курируете вы: текст, описание и роли по режимам."
        empty="Пока нет user-частей. Создайте первую."
        parts={userParts}
        readOnly={false}
        patchCounts={patchCounts}
        onOpenPart={onOpenPart}
        onShowPatches={onShowPatches}
        onRoleError={setRoleError}
        action={
          <Button
            variant="primary"
            icon={<Plus aria-hidden size={14} strokeWidth={2.5} />}
            onClick={onCreatePart}
          >
            Создать часть
          </Button>
        }
      />
    </div>
  );
}

interface ScopeGroupProps {
  title: string;
  hint: string;
  empty: string;
  parts: PromptPartListItem[];
  readOnly: boolean;
  patchCounts?: Map<string, number>;
  onOpenPart: (part: PromptPartListItem) => void;
  onShowPatches?: (part: PromptPartListItem) => void;
  onRoleError: (message: string | null) => void;
  action?: React.ReactNode;
}

function ScopeGroup({
  title,
  hint,
  empty,
  parts,
  readOnly,
  patchCounts,
  onOpenPart,
  onShowPatches,
  onRoleError,
  action
}: ScopeGroupProps) {
  return (
    <section className="flex flex-col gap-2">
      <div className="flex items-center justify-between gap-2">
        <div className="flex flex-col gap-0.5">
          <h3 className="m-0 text-[13px] font-bold uppercase tracking-wider text-base-content/70">
            {title}
          </h3>
          <p className="m-0 text-xs text-base-content/60">{hint}</p>
        </div>
        {action}
      </div>
      {parts.length === 0 ? (
        <p className="m-0 text-[13px] text-base-content/60">{empty}</p>
      ) : (
        <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
          {parts.map((part) => (
            <PartRow
              key={part.id}
              part={part}
              readOnly={readOnly}
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
              onRoleError={onRoleError}
            />
          ))}
        </ul>
      )}
    </section>
  );
}
