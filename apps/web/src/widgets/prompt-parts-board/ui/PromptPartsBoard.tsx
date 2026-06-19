import { useMemo, useState } from "react";

import {
  PROMPT_PART_MODES,
  useListPromptParts,
  type PromptPartListItem
} from "@/entities/prompt-part";
import { errorMessage } from "@/shared/lib";

import { useBundlesTreeQuery } from "../model/use-bundles-tree";
import { McpBundleCompatibility } from "./McpBundleCompatibility";
import { ModeCompositionPanel } from "./ModeCompositionPanel";
import { PartsList } from "./PartsList";
import {
  PromptPartDetailDialog,
  type PromptPartDialogTarget
} from "./PromptPartDetailDialog";

export function PromptPartsBoard() {
  const partsQuery = useListPromptParts();
  const bundlesQuery = useBundlesTreeQuery();

  const [partDialog, setPartDialog] = useState<PromptPartDialogTarget | null>(
    null
  );

  const parts: PromptPartListItem[] = partsQuery.data ?? [];
  const userParts = useMemo(
    () => parts.filter((p) => p.scope === "user"),
    [parts]
  );

  const error = partsQuery.error
    ? errorMessage(partsQuery.error, { base: "Не удалось загрузить данные" })
    : null;
  const loading = partsQuery.isPending;

  return (
    <section
      className="mx-auto flex max-w-5xl flex-col gap-8"
      aria-label="Части промпта"
    >
      <header className="flex flex-col gap-1.5">
        <h1 className="m-0 text-2xl font-bold tracking-tight">Части промпта</h1>
        <p className="m-0 text-sm leading-relaxed text-base-content/70">
          Один список prompt_parts, поделённый по scope. System засеяны из
          манифеста; user курируете вы. Бандл расширяется и ужимается через роли
          частей по режимам.
        </p>
      </header>

      {error ? (
        <p role="alert" className="m-0 text-[13px] text-base-content/60">
          {error}
        </p>
      ) : null}
      {loading ? (
        <p className="m-0 text-[13px] text-base-content/60">Загрузка…</p>
      ) : null}

      {!loading && !error ? (
        <>
          <Section
            title="Части промпта"
            description="Сгруппированы по scope. Роли по режимам (работа / интервью / свободный) — инлайн в ряду части."
          >
            <PartsList
              parts={parts}
              onOpenPart={(part) => {
                setPartDialog({ mode: "detail", part });
              }}
              onCreatePart={() => {
                setPartDialog({ mode: "create" });
              }}
            />
          </Section>

          <Section
            title="Embedded-композиция по режимам"
            description="Что собирает встроенный терминал для каждого режима: эффективный состав, роли и итоговый system_prompt."
          >
            <div className="flex flex-col gap-4">
              {PROMPT_PART_MODES.map((mode) => (
                <ModeCompositionPanel
                  key={mode}
                  mode={mode}
                  bundlesTree={bundlesQuery.data}
                  optionalParts={userParts}
                />
              ))}
            </div>
          </Section>

          <Section
            title="Совместимость MCP get_prompt_bundle"
            description="Что получает внешний агент через MCP get_prompt_bundle(mode) — read-only обзор по всем режимам манифеста (dream сюда не входит: его плейбук захардкожен в кнопке на /improvements). Источник правды — манифест."
          >
            <McpBundleCompatibility bundlesTree={bundlesQuery.data} />
          </Section>
        </>
      ) : null}

      {partDialog ? (
        <PromptPartDetailDialog
          target={partDialog}
          onClose={() => {
            setPartDialog(null);
          }}
        />
      ) : null}
    </section>
  );
}

function Section({
  title,
  description,
  children
}: {
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <section className="flex flex-col gap-3">
      <header className="flex flex-col gap-0.5 border-b border-base-300 pb-2">
        <h2 className="m-0 text-xl font-semibold tracking-tight">{title}</h2>
        <p className="m-0 text-[13px] text-base-content/60">{description}</p>
      </header>
      {children}
    </section>
  );
}
