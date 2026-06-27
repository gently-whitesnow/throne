import { Upload } from "lucide-react";

/**
 * Slot 4 — user-supplied skills. The loading mechanism is out of scope for now;
 * this placeholder keeps the slot in the assembly so the model stays complete.
 */
export function UserSkillsSlot() {
  return (
    <div className="flex items-center gap-3 rounded-md border border-dashed border-base-300 bg-base-200/30 px-4 py-5 text-sm text-base-content/55">
      <span
        aria-hidden
        className="inline-flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-md bg-base-200 text-base-content/40"
      >
        <Upload size={18} strokeWidth={2} />
      </span>
      <p className="m-0 leading-relaxed">
        Подкладываемые скилы поедут рядом со скилами Трона. Механизм загрузки —
        в работе.
      </p>
    </div>
  );
}
