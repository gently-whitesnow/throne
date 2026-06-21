import type { SettingsComponents } from "@/shared/api";

export type SkillModeDefaults =
  SettingsComponents["schemas"]["SkillModeDefaultsDto"];

export type UpdateSkillModeDefaultsRequest =
  SettingsComponents["schemas"]["UpdateSkillModeDefaultsRequest"];

export type SkillModeDefaultEntry =
  SettingsComponents["schemas"]["UpdateSkillModeDefaultEntry"];

export const SKILL_MODE_LABELS: Record<string, string> = {
  interview: "Interview",
  review: "Review",
  work: "Work",
  free: "Free",
  dream: "Dream"
};
