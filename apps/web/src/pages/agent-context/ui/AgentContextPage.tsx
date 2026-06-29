import { usePartPatchCounts } from "../model/use-part-patch-counts";

import { SlotCountMarker } from "./SlotSection";
import { SystemSlot } from "./SystemSlot";

/**
 * `/agent-context` — редактор системного промпта агента. Прочие слоты состава
 * (User-промпт, скилы) пока скрыты как нередактируемые; на экране остаётся
 * единственное, что оператор реально меняет — блоки system-промпта по режимам.
 * Improvements / История встроены сюда же, рядом с блоками, что они правят.
 */
export function AgentContextPage() {
  const patches = usePartPatchCounts();

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 px-7 pb-16 pt-6">
      <header className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="m-0 text-2xl font-bold tracking-tight">
            System-промпт
          </h1>
          {patches.total > 0 ? <SlotCountMarker value={patches.total} /> : null}
        </div>
        <p className="m-0 max-w-[72ch] text-sm leading-relaxed text-base-content/70">
          Системный промпт агента — это набор блоков инструкций. Что именно
          уйдёт в запуск, зависит от выбранного режима: выбери режим и увидишь
          его состав.
        </p>
        <dl className="m-0 flex flex-col gap-1 text-xs leading-relaxed">
          <div className="flex gap-1.5">
            <dt className="font-semibold text-base-content/60">источник:</dt>
            <dd className="m-0 text-base-content/60">
              системные блоки — из манифеста (read-only); твои — создаёшь и
              правишь здесь
            </dd>
          </div>
          <div className="flex gap-1.5">
            <dt className="font-semibold text-base-content/60">где менять:</dt>
            <dd className="m-0 text-base-content/50">
              выбери режим слева → включай/выключай блоки и правь текст своих
            </dd>
          </div>
        </dl>
      </header>

      <SystemSlot patchCounts={patches.counts} proposedTotal={patches.total} />
    </div>
  );
}
