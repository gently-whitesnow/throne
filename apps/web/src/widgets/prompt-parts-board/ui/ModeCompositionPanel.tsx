import { useQueries } from "@tanstack/react-query";

import {
  getPromptPart,
  promptPartsQueryKeys,
  PROMPT_PART_MODE_LABELS,
  type PromptPartListItem,
  type PromptPartMode
} from "@/entities/prompt-part";

import type { BundlesTreeData } from "../model/use-bundles-tree";
import {
  assembleSystemPrompt,
  buildModeComposition,
  type EffectivePart
} from "../model/composition";
import { ModeCompositionRow } from "./ModeCompositionRow";

interface ModeCompositionPanelProps {
  mode: PromptPartMode;
  bundlesTree: BundlesTreeData | undefined;
  optionalParts: PromptPartListItem[];
}

/**
 * Effective embedded composition for one mode: ordered parts (mandatory →
 * optional), inline role editing for optional user parts, and the assembled
 * system_prompt preview. Full optional texts are fetched so the preview matches
 * the backend resolver byte-for-byte.
 */
export function ModeCompositionPanel({
  mode,
  bundlesTree,
  optionalParts
}: ModeCompositionPanelProps) {
  // Optional parts that hold any role in this mode — we need their full text.
  const presentOptionalIds = optionalParts
    .filter((p) => p.mode_roles.some((r) => r.mode === mode))
    .map((p) => p.id);

  const detailQueries = useQueries({
    queries: presentOptionalIds.map((id) => ({
      queryKey: promptPartsQueryKeys.detail(id),
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        getPromptPart(id, signal)
    }))
  });

  const optionalTexts: Record<string, string> = {};
  detailQueries.forEach((q, index) => {
    const id = presentOptionalIds[index];
    if (q.data) {
      optionalTexts[id] = q.data.text;
    }
  });
  const allTextsLoaded = detailQueries.every((q) => q.data !== undefined);

  const parts = buildModeComposition(
    mode,
    bundlesTree,
    optionalParts,
    optionalTexts
  );
  const systemPrompt = assembleSystemPrompt(parts);

  return (
    <section className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 p-4">
      <header className="flex flex-col gap-0.5">
        <h3 className="m-0 text-base font-semibold text-base-content">
          {PROMPT_PART_MODE_LABELS[mode]}
        </h3>
        <p className="m-0 text-xs text-base-content/60">
          Что собирает встроенный терминал в режиме «
          {PROMPT_PART_MODE_LABELS[mode]}».
        </p>
      </header>

      <ul className="m-0 flex list-none flex-col gap-1 p-0">
        {parts.length === 0 ? (
          <li className="text-[13px] text-base-content/60">
            Нет частей в композиции.
          </li>
        ) : (
          parts.map((part: EffectivePart) => (
            <ModeCompositionRow
              key={`${part.kind}:${part.partId}`}
              part={part}
              mode={mode}
              optionalParts={optionalParts}
            />
          ))
        )}
      </ul>

      <div className="flex flex-col gap-1">
        <span className="text-[13px] font-bold uppercase tracking-wider text-base-content/70">
          Собранный system_prompt
        </span>
        {!allTextsLoaded ? (
          <p className="m-0 text-xs text-warning">
            Тексты optional-частей ещё подгружаются — превью может быть
            неполным.
          </p>
        ) : null}
        <pre className="m-0 max-h-72 overflow-auto whitespace-pre-wrap break-words rounded-md border border-base-300 bg-base-200 px-4 py-3 font-mono text-xs leading-relaxed">
          {systemPrompt || "— пусто —"}
        </pre>
      </div>
    </section>
  );
}
