import { Lock, Sprout } from "lucide-react";

import {
  PROMPT_PART_MODES,
  PROMPT_PART_MODE_LABELS,
  PROMPT_PART_ROLE_LABELS,
  type PromptPartListItem,
  type PromptPartUiRole
} from "@/entities/prompt-part";

import { RoleSelect } from "./RoleSelect";

function ScopeBadge({ scope }: { scope: string }) {
  const isSystem = scope === "system";
  return (
    <span
      className={`inline-flex h-[18px] items-center rounded-full px-2 text-[10px] font-bold uppercase tracking-wide ${
        isSystem
          ? "bg-base-300 text-base-content/70"
          : "bg-primary/10 text-primary"
      }`}
    >
      {scope}
    </span>
  );
}

interface PartRowProps {
  part: PromptPartListItem;
  /** System parts are manifest-managed: text and roles are read-only. */
  readOnly: boolean;
  /** Proposed-patch count for this part; renders the «N правок» affordance. */
  patchCount?: number;
  onOpen: () => void;
  onShowPatches?: () => void;
  onRoleError: (message: string | null) => void;
}

/**
 * One prompt-part row: scope badge, key, preview, and per-mode roles. User parts
 * expose an inline RoleSelect; system parts show their (read-only) role for each
 * embedded mode. Clicking the body opens the detail dialog; the «N правок»
 * badge surfaces the part's proposed improvements next to the part itself.
 */
export function PartRow({
  part,
  readOnly,
  patchCount = 0,
  onOpen,
  onShowPatches,
  onRoleError
}: PartRowProps) {
  return (
    <li className="flex flex-col gap-2 rounded-md border border-base-300 bg-base-100 px-3 py-2.5">
      <div className="flex items-start justify-between gap-2">
        <button
          type="button"
          className="flex min-w-0 flex-col items-start gap-1 text-left focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-2"
          onClick={onOpen}
        >
          <span className="flex items-center gap-2">
            <ScopeBadge scope={part.scope} />
            <span className="font-mono text-[13px] font-semibold text-base-content">
              {part.key}
            </span>
            {readOnly ? (
              <span className="inline-flex h-[18px] items-center gap-1 text-[10px] font-medium uppercase tracking-wide text-base-content/40">
                <Lock aria-hidden size={10} strokeWidth={2.5} />
                из манифеста
              </span>
            ) : null}
          </span>
          {part.description ? (
            <span className="text-xs text-base-content/70">
              {part.description}
            </span>
          ) : null}
          <span className="line-clamp-2 font-mono text-xs text-base-content/50">
            {part.text_short}
          </span>
        </button>
        {patchCount > 0 && onShowPatches ? (
          <button
            type="button"
            onClick={onShowPatches}
            className="inline-flex h-[22px] flex-shrink-0 items-center gap-1 rounded-full bg-warning-soft px-2 text-[11px] font-semibold text-warning transition-[color,background-color,scale] hover:bg-warning/20 active:scale-[0.96]"
            title="Предложенные правки этой части"
          >
            <Sprout aria-hidden size={11} strokeWidth={2.5} />
            <span className="tabular-nums">{patchCount}</span>
            <span>{pluralPravki(patchCount)}</span>
          </button>
        ) : null}
      </div>
      <div className="flex flex-wrap gap-3 border-t border-base-200 pt-2">
        {PROMPT_PART_MODES.map((mode, index) => (
          <span key={mode} className="flex items-center gap-1.5">
            <span className="text-xs font-medium text-base-content/60">
              {PROMPT_PART_MODE_LABELS[mode]}
            </span>
            {readOnly ? (
              <ReadOnlyRole part={part} mode={mode} />
            ) : (
              <RoleSelect
                part={part}
                mode={mode}
                orderForNew={index}
                onError={onRoleError}
              />
            )}
          </span>
        ))}
      </div>
    </li>
  );
}

/** Russian plural for «правка» (1 правка / 2 правки / 5 правок). */
function pluralPravki(n: number): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return "правка";
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return "правки";
  return "правок";
}

function ReadOnlyRole({
  part,
  mode
}: {
  part: PromptPartListItem;
  mode: (typeof PROMPT_PART_MODES)[number];
}) {
  const role: PromptPartUiRole =
    (part.mode_roles.find((r) => r.mode === mode)?.role as
      | PromptPartUiRole
      | undefined) ?? "none";
  return (
    <span className="inline-flex h-[22px] items-center rounded-md border border-base-200 px-2 text-xs font-medium text-base-content/70">
      {PROMPT_PART_ROLE_LABELS[role]}
    </span>
  );
}
