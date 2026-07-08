import { PackagePlus } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { Button } from "@/shared/ui";

import type { AvailableSessionSkill } from "../model/types";

interface SkillsAttachControlProps {
  available: readonly AvailableSessionSkill[];
  /** True when a live tmux session can accept hot-attach. */
  sessionLive: boolean;
  /** True while previewIntentTerminal is loading the catalog. */
  isLoadingAvailable: boolean;
  /** True while POST /skills/attach is in flight. */
  isAttaching: boolean;
  onAttach: (skillIds: string[]) => void;
}

/**
 * Скилы, загруженные в живую сессию текущего режима. Единственный источник
 * «загружено» — `skill.selected` из preview (== selected_skill_ids_by_mode[mode]):
 * сюда попадает и выбор на спавне, и hot-attach. Бейджи рисуем по выбранным
 * скилам, кнопка-попап «Скилы» активна только при live-сессии: уже загруженный
 * скил появляется в попапе как disabled + checked (снять нельзя — append-only).
 */
export function SkillsAttachControl({
  available,
  sessionLive,
  isLoadingAvailable,
  isAttaching,
  onAttach
}: SkillsAttachControlProps) {
  const [open, setOpen] = useState(false);
  const [selectedNew, setSelectedNew] = useState<string[]>([]);
  const rootRef = useRef<HTMLDivElement | null>(null);

  const loadedSkills = useMemo(
    () => available.filter((s) => s.selected),
    [available]
  );

  useEffect(() => {
    if (!open) return;
    const handler = (event: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => {
      document.removeEventListener("mousedown", handler);
    };
  }, [open]);

  useEffect(() => {
    if (!sessionLive) {
      setOpen(false);
      setSelectedNew([]);
    }
  }, [sessionLive]);

  const toggleNew = useCallback((skillId: string) => {
    setSelectedNew((prev) =>
      prev.includes(skillId)
        ? prev.filter((id) => id !== skillId)
        : [...prev, skillId]
    );
  }, []);

  const submit = useCallback(() => {
    if (selectedNew.length === 0) return;
    onAttach(selectedNew);
    setSelectedNew([]);
    setOpen(false);
  }, [selectedNew, onAttach]);

  const buttonDisabled = !sessionLive || isAttaching;
  const buttonTitle = sessionLive
    ? null
    : "Запустите сессию, чтобы догружать скилы";

  return (
    <div
      ref={rootRef}
      className="relative flex flex-wrap items-center gap-1.5"
      data-testid="agent-terminal-skills-attach"
    >
      {loadedSkills.map((skill) => (
        <span
          key={skill.skill_id}
          className="badge badge-sm badge-outline gap-1 text-[11px]"
          data-testid={`agent-terminal-skill-badge-${skill.skill_id}`}
          title="Скил загружен в сессию"
        >
          {skill.title}
        </span>
      ))}

      <Button
        data-testid="agent-terminal-skills-attach-toggle"
        className="btn-xs btn-ghost"
        icon={<PackagePlus aria-hidden size={12} strokeWidth={2} />}
        disabled={buttonDisabled}
        title={buttonTitle ?? undefined}
        onClick={() => {
          setOpen((value) => !value);
        }}
      >
        Скилы
      </Button>

      {open && sessionLive ? (
        <div
          role="dialog"
          aria-label="Догрузить скилы в сессию"
          className="absolute right-0 top-full z-20 mt-1 flex w-72 flex-col gap-2 rounded-md border border-base-300 bg-base-100 p-2 shadow-lg"
        >
          {isLoadingAvailable ? (
            <span className="text-[11px] text-base-content/60">
              Загружаем список скилов…
            </span>
          ) : available.length === 0 ? (
            <span className="text-[11px] text-base-content/60">
              Каталог скилов пуст.
            </span>
          ) : (
            <ul className="flex flex-col gap-1.5">
              {available.map((skill) => {
                const alreadyAttached = skill.selected;
                const checked =
                  alreadyAttached || selectedNew.includes(skill.skill_id);
                const disabled = alreadyAttached || !skill.materializable;
                const reason = alreadyAttached
                  ? "Скил уже загружен в сессию (снять нельзя)."
                  : !skill.materializable
                    ? (skill.reason ?? "Скил недоступен для текущего интента.")
                    : skill.description;
                return (
                  <li key={skill.skill_id}>
                    <label className="flex items-start gap-2 text-xs text-base-content/80">
                      <input
                        type="checkbox"
                        className="checkbox checkbox-xs mt-0.5"
                        aria-label={skill.title}
                        checked={checked}
                        disabled={disabled}
                        onChange={() => {
                          toggleNew(skill.skill_id);
                        }}
                      />
                      <span className="flex min-w-0 flex-col gap-0.5">
                        <span className="font-medium text-base-content">
                          {skill.title}
                          {alreadyAttached ? (
                            <span className="ml-1 text-[10px] text-base-content/50">
                              · загружен
                            </span>
                          ) : null}
                        </span>
                        <span className="text-[11px] leading-relaxed text-base-content/60">
                          {reason}
                        </span>
                      </span>
                    </label>
                  </li>
                );
              })}
            </ul>
          )}

          <div className="flex justify-end gap-2">
            <Button
              className="btn-xs btn-ghost"
              onClick={() => {
                setOpen(false);
                setSelectedNew([]);
              }}
            >
              Отмена
            </Button>
            <Button
              data-testid="agent-terminal-skills-attach-submit"
              className="btn-xs"
              variant="primary"
              disabled={selectedNew.length === 0 || isAttaching}
              onClick={submit}
            >
              {isAttaching ? "Прилагаем…" : "Применить"}
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
