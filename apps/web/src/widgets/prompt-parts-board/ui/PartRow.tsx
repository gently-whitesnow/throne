import { Lock } from "lucide-react";

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
  onOpen: () => void;
  onRoleError: (message: string | null) => void;
}

/**
 * One prompt-part row: scope badge, key, preview, and per-mode roles. User parts
 * expose an inline RoleSelect; system parts show their (read-only) role for each
 * embedded mode. Clicking the body opens the detail dialog.
 */
export function PartRow({ part, readOnly, onOpen, onRoleError }: PartRowProps) {
  return (
    <li className="flex flex-col gap-2 rounded-md border border-base-300 bg-base-100 px-3 py-2.5">
      <button
        type="button"
        className="flex flex-col items-start gap-1 text-left focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-2"
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
