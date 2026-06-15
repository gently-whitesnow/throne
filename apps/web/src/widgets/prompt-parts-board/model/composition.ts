import type { PromptPartsComponents } from "@/shared/api";
import type {
  PromptPartListItem,
  PromptPartMode,
  PromptPartModeRole,
  PromptPartRole,
  PromptPartUiRole
} from "@/entities/prompt-part";

type BundlesTree = PromptPartsComponents["schemas"]["BundlesTreeDto"];

/**
 * One resolved entry in a mode's effective embedded composition. Mirrors the
 * backend `EffectivePart` (PromptCompositionResolver) so the frontend preview
 * lines up with what the embedded terminal assembles.
 */
export interface EffectivePart {
  partId: string;
  key: string;
  scope: string;
  role: PromptPartRole;
  order: number;
  editable: boolean;
  present: boolean;
  selected: boolean;
  text: string;
  /** Whether the full text is known (false for optionals built from text_short). */
  textIsFull: boolean;
  kind: "mandatory" | "optional";
}

/**
 * Builds the effective composition for a mode: mandatory instruction parts
 * projected from the bundles tree (system→user, already ordered) followed by
 * optional user parts that hold a role in the mode, sorted by (order, key).
 *
 * `optionalTexts` optionally supplies full part texts (keyed by id); when an
 * id is missing the part falls back to its `text_short` with textIsFull=false.
 */
export function buildModeComposition(
  mode: PromptPartMode,
  bundlesTree: BundlesTree | undefined,
  optionalParts: PromptPartListItem[],
  optionalTexts?: Record<string, string>
): EffectivePart[] {
  const mandatory = buildMandatory(mode, bundlesTree);
  // A user part that is a manifest bundle include (e.g. user:work) already shows
  // as a mandatory row; skipping it among optionals mirrors the backend resolver,
  // which dedups on partId so the part isn't listed — and its role isn't toggled —
  // twice. Without this the preview both duplicates the part and renders a role
  // dropdown that the bundle path ignores.
  const mandatoryIds = new Set(mandatory.map((p) => p.partId));
  const optional = buildOptional(mode, optionalParts, optionalTexts).filter(
    (p) => !mandatoryIds.has(p.partId)
  );
  return mandatory.concat(optional);
}

function buildMandatory(
  mode: PromptPartMode,
  bundlesTree: BundlesTree | undefined
): EffectivePart[] {
  // free curates everything by hand — no mandatory parts.
  if (mode === "free") return [];
  const includes =
    bundlesTree?.bundles.find((b) => b.mode === mode)?.includes ?? [];
  return includes.map((entry, index) => ({
    partId: entry.prompt_part_id ?? `part:${entry.scope}:${entry.key}`,
    key: entry.key,
    scope: entry.scope,
    role: "mandatory" as const,
    order: index,
    editable: entry.editable,
    present: entry.present,
    selected: true,
    text: entry.text,
    textIsFull: true,
    kind: "mandatory" as const
  }));
}

function buildOptional(
  mode: PromptPartMode,
  optionalParts: PromptPartListItem[],
  optionalTexts?: Record<string, string>
): EffectivePart[] {
  const parts: EffectivePart[] = [];
  for (const part of optionalParts) {
    const role = part.mode_roles.find((r) => r.mode === mode);
    if (!role) continue;
    const fullText = optionalTexts?.[part.id];
    parts.push({
      partId: part.id,
      key: part.key,
      scope: part.scope,
      role: role.role as PromptPartRole,
      order: role.order,
      editable: true,
      present: true,
      selected: role.role === "mandatory" || role.role === "default_on",
      text: fullText ?? part.text_short,
      textIsFull: fullText !== undefined,
      kind: "optional" as const
    });
  }
  return parts.sort((a, b) =>
    a.order !== b.order
      ? a.order - b.order
      : a.key < b.key
        ? -1
        : a.key > b.key
          ? 1
          : 0
  );
}

/**
 * Assembles the system_prompt exactly like the backend resolver: selected parts
 * only, each text trimmed, empties dropped, joined by a blank line.
 */
export function assembleSystemPrompt(parts: EffectivePart[]): string {
  return parts
    .filter((p) => p.selected)
    .map((p) => p.text.trim())
    .filter((t) => t.length > 0)
    .join("\n\n");
}

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
