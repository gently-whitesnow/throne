import { SkillModeDefaultsCard } from "@/widgets/skill-mode-defaults-card";

/**
 * Slot 3 — Throne skills. The mode×skill matrix is the launch default; the slot
 * header owns the title, so the card renders headerless.
 */
export function SkillsSlot() {
  return <SkillModeDefaultsCard hideHeader />;
}
