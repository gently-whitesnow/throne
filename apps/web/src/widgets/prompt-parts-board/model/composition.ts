import type {
  PromptPartMode,
  PromptPartModeRole,
  PromptPartUiRole
} from "@/entities/prompt-part";

/**
 * Produces the whole-replace mode_roles list for setPromptPartRoles after
 * changing a single mode's role. "none" detaches the mode; otherwise the mode
 * entry is upserted (keeping its existing order, or `orderForNew`). Roles of
 * other modes are preserved unchanged.
 */
export function mergeRoleForMode(
  modeRoles: PromptPartModeRole[],
  mode: PromptPartMode,
  nextRole: PromptPartUiRole,
  orderForNew: number
): PromptPartModeRole[] {
  const others = modeRoles.filter((r) => r.mode !== mode);
  if (nextRole === "none") {
    return others;
  }
  const existing = modeRoles.find((r) => r.mode === mode);
  const entry: PromptPartModeRole = {
    mode,
    role: nextRole,
    order: existing?.order ?? orderForNew
  };
  return [...others, entry];
}
