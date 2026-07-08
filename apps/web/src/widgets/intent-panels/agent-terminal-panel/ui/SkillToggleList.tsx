import { PackageCheck } from "lucide-react";

import type { AvailableSessionSkill } from "../model/types";

interface SkillToggleListProps {
  skills: AvailableSessionSkill[];
  onToggle: (skillId: string) => void;
}

export function SkillToggleList({ skills, onToggle }: SkillToggleListProps) {
  if (skills.length === 0) return null;

  return (
    <section className="flex flex-col gap-2 rounded-md border border-base-300 bg-base-100 px-2 py-2">
      <div className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wide text-base-content/55">
        <PackageCheck aria-hidden size={13} strokeWidth={2} />
        <span>Скилы сессии</span>
      </div>
      <div className="flex flex-col gap-2">
        {skills.map((skill) => (
          <label
            key={skill.skill_id}
            className="flex items-start gap-2 text-xs text-base-content/75"
          >
            <input
              type="checkbox"
              className="checkbox checkbox-xs mt-0.5"
              aria-label={skill.title}
              checked={skill.selected}
              disabled={!skill.materializable}
              onChange={() => {
                onToggle(skill.skill_id);
              }}
            />
            <span className="flex min-w-0 flex-col gap-0.5">
              <span className="font-medium text-base-content">
                {skill.title}
              </span>
              <span className="text-[11px] leading-relaxed text-base-content/60">
                {skill.materializable
                  ? skill.description
                  : (skill.reason ?? "Недоступно для текущего интента")}
              </span>
            </span>
          </label>
        ))}
      </div>
    </section>
  );
}
