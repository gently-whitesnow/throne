import { useState } from "react";

import { DreamsTab } from "@/widgets/improvements-dreams-tab";
import { PatchesTab } from "@/widgets/improvements-patches-tab";

type TabKey = "patches" | "dreams";

const TABS: { id: TabKey; label: string }[] = [
  { id: "patches", label: "Patches" },
  { id: "dreams", label: "Dreams" }
];

/**
 * `/improvements` — две вкладки:
 *   * Patches — PromptPartPatch list + diff preview + apply / apply-with-edit / reject;
 *   * Dreams — история проходов фронтир-агента (DreamSession) + dream_sources.
 *
 * Dream запускается через /dream (скилл skills/dream/SKILL.md) — единственный
 * источник плейбука; см. ADR-0022 (dream-flow), ADR-0043 (static skills).
 */
export function ImprovementsSectionPage() {
  const [tab, setTab] = useState<TabKey>("patches");

  return (
    <div className="w-full px-7 pb-12 pt-6">
      <section
        className="mx-auto flex max-w-6xl flex-col gap-6"
        aria-label="Improvements"
      >
        <header className="flex flex-col gap-2">
          <h2 className="m-0 text-xl font-bold tracking-tight">Improvements</h2>
          <p className="m-0 text-sm leading-relaxed text-base-content/70">
            Patches — конкретные изменения user-инструкций, которые предложил
            фронтир-агент. Apply / apply-with-edit / reject — твоё решение.
            Dreams — история проходов: что разбирали и какие правки родились.
          </p>
        </header>

        <div role="tablist" className="tabs tabs-boxed w-fit">
          {TABS.map((t) => (
            <button
              key={t.id}
              role="tab"
              type="button"
              aria-selected={tab === t.id}
              className={tab === t.id ? "tab tab-active" : "tab"}
              onClick={() => {
                setTab(t.id);
              }}
            >
              {t.label}
            </button>
          ))}
        </div>

        <div role="tabpanel">
          {tab === "patches" ? <PatchesTab /> : <DreamsTab />}
        </div>
      </section>
    </div>
  );
}
