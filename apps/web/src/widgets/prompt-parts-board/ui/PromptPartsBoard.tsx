import { useMemo, useState } from "react";

import {
  PROMPT_PART_MODES,
  useListPromptParts,
  type PromptPartListItem
} from "@/entities/prompt-part";
import { HttpError } from "@/shared/api";

import {
  dedupeProjectedInstructions,
  type ProjectedInstruction
} from "../model/composition";
import { useBundlesTreeQuery } from "../model/use-bundles-tree";
import { InstructionEditDialog } from "./InstructionEditDialog";
import { McpBundleCompatibility } from "./McpBundleCompatibility";
import { ModeCompositionPanel } from "./ModeCompositionPanel";
import { PartsList } from "./PartsList";
import {
  PromptPartDetailDialog,
  type PromptPartDialogTarget
} from "./PromptPartDetailDialog";

export function PromptPartsBoard() {
  const bundlesQuery = useBundlesTreeQuery();
  const partsQuery = useListPromptParts();

  const [partDialog, setPartDialog] = useState<PromptPartDialogTarget | null>(
    null
  );
  const [instructionDialog, setInstructionDialog] =
    useState<ProjectedInstruction | null>(null);

  const instructions = useMemo(
    () => dedupeProjectedInstructions(bundlesQuery.data),
    [bundlesQuery.data]
  );
  const optionalParts: PromptPartListItem[] = partsQuery.data ?? [];

  const error =
    (bundlesQuery.error ?? partsQuery.error)
      ? errorMessage(bundlesQuery.error ?? partsQuery.error)
      : null;
  const loading = bundlesQuery.isPending || partsQuery.isPending;

  return (
    <section
      className="mx-auto flex max-w-5xl flex-col gap-8"
      aria-label="Части промпта"
    >
      <header className="flex flex-col gap-1.5">
        <h1 className="m-0 text-2xl font-bold tracking-tight">Части промпта</h1>
        <p className="m-0 text-sm leading-relaxed text-base-content/70">
          Управление частями промпта и их ролями по режимам встроенного
          терминала. Mandatory-инструкции проецируются из манифеста бандлов;
          optional-части курируете вы.
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
            description="Все части: спроецированные инструкции (mandatory) и ваши optional-части с ролями по режимам."
          >
            <PartsList
              instructions={instructions}
              optionalParts={optionalParts}
              onOpenInstruction={setInstructionDialog}
              onOpenOptional={(part) => {
                setPartDialog({ mode: "edit", part });
              }}
              onCreateOptional={() => {
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
                  optionalParts={optionalParts}
                />
              ))}
            </div>
          </Section>

          <Section
            title="Совместимость MCP get_prompt_bundle"
            description="Что получает внешний агент через MCP get_prompt_bundle(mode) — read-only обзор по всем режимам (включая dream/fix). Источник правды — манифест."
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
      {instructionDialog ? (
        <InstructionEditDialog
          instruction={instructionDialog}
          onClose={() => {
            setInstructionDialog(null);
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

function errorMessage(err: unknown): string {
  if (err instanceof HttpError) {
    return `Не удалось загрузить данные (${String(err.status)}).`;
  }
  return "Не удалось загрузить данные.";
}
