import { usePartPatchCounts } from "../model/use-part-patch-counts";

import { SkillsSlot } from "./SkillsSlot";
import { SlotRail, type SlotRailItem } from "./SlotRail";
import { SlotCountMarker, SlotSection, SoonChip } from "./SlotSection";
import { SystemSlot } from "./SystemSlot";
import { UserPromptSlot } from "./UserPromptSlot";
import { UserSkillsSlot } from "./UserSkillsSlot";

/**
 * `/agent-context` — единый хаб «Состав агента». Заменяет Prompt parts / Launch
 * skills / Improvements: один экран, собранный по метафоре «что уйдёт агенту».
 * Четыре слота читаются сверху вниз как сборка прогона; в каждом — что это и
 * где менять. Improvements встроены в слот 1 рядом с частями.
 */
export function AgentContextPage() {
  const patches = usePartPatchCounts();

  const railItems: SlotRailItem[] = [
    {
      id: "slot-system",
      index: 1,
      label: "System · блоки",
      badge: patches.total
    },
    { id: "slot-user-prompt", index: 2, label: "User-промпт" },
    { id: "slot-skills", index: 3, label: "Скилы" },
    { id: "slot-user-skills", index: 4, label: "Скилы юзера" }
  ];

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-8 px-7 pb-16 pt-6">
      <header className="flex flex-col gap-2">
        <h1 className="m-0 text-2xl font-bold tracking-tight">Состав агента</h1>
        <p className="m-0 max-w-[72ch] text-sm leading-relaxed text-base-content/70">
          Всё, что уходит агенту при запуске, собрано здесь. Читай сверху вниз —
          это сборка прогона: в каждом слоте видно, откуда он берётся и где его
          менять.
        </p>
      </header>

      <SlotRail items={railItems} />

      <SlotSection
        id="slot-system"
        index={1}
        title="System-промпт"
        source="блоки инструкций; что войдёт — зависит от режима запуска"
        whereToChange="выбери режим слева → правь состав и текст своих блоков; системные read-only из манифеста"
        marker={
          patches.total > 0 ? <SlotCountMarker value={patches.total} /> : null
        }
      >
        <SystemSlot
          patchCounts={patches.counts}
          proposedTotal={patches.total}
        />
      </SlotSection>

      <SlotSection
        id="slot-user-prompt"
        index={2}
        title="User-промпт"
        source="тело интента + свободная вставка оператора"
        whereToChange="тело — на странице интента; вставка — в окне запуска"
      >
        <UserPromptSlot />
      </SlotSection>

      <SlotSection
        id="slot-skills"
        index={3}
        title="Скилы"
        source="дефолтные скилы Трона по режимам (intent / review / dream)"
        whereToChange="здесь — матрица режим × скил; на сессию — в окне запуска"
      >
        <SkillsSlot />
      </SlotSection>

      <SlotSection
        id="slot-user-skills"
        index={4}
        title="Скилы юзера"
        source="подкладываемые пользовательские скилы"
        marker={<SoonChip />}
        muted
      >
        <UserSkillsSlot />
      </SlotSection>
    </div>
  );
}
