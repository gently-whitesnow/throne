import type { PromptPartsComponents } from "@/shared/api";

export type PromptPart = PromptPartsComponents["schemas"]["PromptPartDto"];
export type PromptPartListItem =
  PromptPartsComponents["schemas"]["PromptPartListItemDto"];
export type PromptPartModeRole =
  PromptPartsComponents["schemas"]["PromptPartModeRoleDto"];
export type CreatePromptPartRequest =
  PromptPartsComponents["schemas"]["CreatePromptPartRequest"];
export type SetPromptPartRolesRequest =
  PromptPartsComponents["schemas"]["SetPromptPartRolesRequest"];
export type DeletePromptPartResponse =
  PromptPartsComponents["schemas"]["DeletePromptPartResponse"];

/** Embedded composition modes managed via prompt-parts roles. */
export const PROMPT_PART_MODES = [
  "work",
  "interview",
  "review",
  "free"
] as const;
export type PromptPartMode = (typeof PROMPT_PART_MODES)[number];

export const PROMPT_PART_MODE_LABELS: Record<PromptPartMode, string> = {
  work: "Работа",
  interview: "Интервью",
  review: "Review",
  free: "Свободный"
};

/** Backend roles plus the UI-only "none" (part has no role in the mode). */
export const PROMPT_PART_ROLES = [
  "mandatory",
  "default_on",
  "default_off"
] as const;
export type PromptPartRole = (typeof PROMPT_PART_ROLES)[number];

export type PromptPartUiRole = PromptPartRole | "none";

/**
 * Per-mode role wording, phrased as «what happens in this mode» so the operator
 * reads membership, not the backend enum: mandatory always ships, default_on
 * ships unless toggled off, default_off is available-but-off, none is excluded.
 */
export const PROMPT_PART_ROLE_LABELS: Record<PromptPartUiRole, string> = {
  mandatory: "всегда",
  default_on: "по умолчанию вкл",
  default_off: "доступен, выкл",
  none: "не входит"
};

/** Roles selectable for an optional part within a single mode. */
export const PROMPT_PART_UI_ROLE_OPTIONS: PromptPartUiRole[] = [
  "none",
  "default_off",
  "default_on",
  "mandatory"
];
