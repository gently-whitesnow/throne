import { Check, Copy } from "lucide-react";
import { useState } from "react";

import { DreamsTab } from "@/widgets/improvements-dreams-tab";
import { PatchesTab } from "@/widgets/improvements-patches-tab";

type TabKey = "patches" | "dreams";

const TABS: { id: TabKey; label: string }[] = [
  { id: "patches", label: "Patches" },
  { id: "dreams", label: "Dreams" }
];

const DREAM_PROMPT =
  "Запусти dream — улучшим мои user-инструкции по моим последним диалогам. " +
  'Зачитай бандл dream через MCP get_instruction_bundle({mode: "dream"}) и следуй инструкции.';

/**
 * `/improvements` — две вкладки:
 *   * Patches — InstructionPatch list + diff preview + apply / apply-with-edit / reject;
 *   * Dreams — история проходов фронтир-агента (DreamSession) + dream_sources.
 *
 * Кнопка «Скопировать промпт» отдаёт подсказку для запуска dream-прохода в
 * подключённом агенте. Сам разбор делает фронтир локально через Read/Glob, см.
 * ADR-0022.
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
          <div className="flex items-center gap-3">
            <h2 className="m-0 text-xl font-bold tracking-tight">
              Improvements
            </h2>
            <CopyPromptButton />
          </div>
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

function CopyPromptButton() {
  const [copied, setCopied] = useState(false);

  return (
    <button
      type="button"
      className="btn btn-xs btn-outline"
      onClick={() => {
        void navigator.clipboard.writeText(DREAM_PROMPT).then(() => {
          setCopied(true);
          window.setTimeout(() => {
            setCopied(false);
          }, 1500);
        });
      }}
      aria-label="Скопировать промпт для запуска dream"
      title="Скопировать промпт для запуска dream"
    >
      {copied ? (
        <>
          <Check aria-hidden size={14} strokeWidth={2} /> Скопировано
        </>
      ) : (
        <>
          <Copy aria-hidden size={14} strokeWidth={2} /> Промпт для dream
        </>
      )}
    </button>
  );
}
