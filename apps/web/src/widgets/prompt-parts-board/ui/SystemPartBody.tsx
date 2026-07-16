import {
  PROMPT_PART_MODES,
  PROMPT_PART_MODE_LABELS,
  PROMPT_PART_ROLE_LABELS,
  usePromptPart,
  type PromptPartListItem,
  type PromptPartUiRole
} from "@/entities/prompt-part";
import { Button } from "@/shared/ui";

/**
 * Read-only view of a system prompt part. System parts are read from the
 * manifest, so neither text nor roles are editable here — they change only via
 * the manifest.
 */
export function SystemPartBody({
  part,
  onClose
}: {
  part: PromptPartListItem;
  onClose: () => void;
}) {
  const detail = usePromptPart(part.id);

  return (
    <div className="flex flex-col gap-4">
      <p className="m-0 text-xs text-base-content/60">
        Системный блок управляется манифестом — меняется только через него.
      </p>

      <section className="flex flex-col gap-1.5">
        <span className="text-[13px] font-semibold text-base-content">
          Где входит по режимам
        </span>
        <div className="flex flex-wrap gap-3">
          {PROMPT_PART_MODES.map((mode) => {
            const role: PromptPartUiRole =
              (part.mode_roles.find((r) => r.mode === mode)?.role as
                PromptPartUiRole | undefined) ?? "none";
            return (
              <span key={mode} className="flex items-center gap-1.5 text-xs">
                <span className="font-medium text-base-content/60">
                  {PROMPT_PART_MODE_LABELS[mode]}
                </span>
                <span className="inline-flex h-[22px] items-center rounded-md border border-base-200 px-2 font-medium text-base-content/70">
                  {PROMPT_PART_ROLE_LABELS[role]}
                </span>
              </span>
            );
          })}
        </div>
      </section>

      <section className="flex flex-col gap-1.5">
        <span className="text-[13px] font-semibold text-base-content">
          Текст
        </span>
        {detail.isPending ? (
          <p className="m-0 text-[13px] text-base-content/60">
            Загрузка текста…
          </p>
        ) : (
          <pre className="m-0 max-h-96 overflow-auto whitespace-pre-wrap break-words rounded-md border border-base-300 bg-base-200 px-4 py-3.5 font-mono text-xs leading-relaxed">
            {detail.data?.text ?? ""}
          </pre>
        )}
      </section>

      <div className="flex justify-end">
        <Button type="button" onClick={onClose}>
          Закрыть
        </Button>
      </div>
    </div>
  );
}
