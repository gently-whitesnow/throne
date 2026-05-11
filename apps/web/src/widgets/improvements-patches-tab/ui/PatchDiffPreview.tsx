import type { InstructionPatchDetail } from "@/entities/instruction-patch";

/**
 * Whole-text side-by-side preview: current Instruction.text vs the proposed
 * patch text. Per ADR-0021 the apply path replaces the entire text — there is
 * no per-line diff in this iteration; the side-by-side rendering is enough for
 * the operator to spot the change before applying.
 */
export function PatchDiffPreview({
  detail
}: {
  detail: InstructionPatchDetail;
}) {
  return (
    <div className="grid gap-3 md:grid-cols-2">
      <PatchPane
        title={`Current (v${String(detail.current_instruction_version)})`}
        text={detail.current_instruction_text}
      />
      <PatchPane title="Proposed" text={detail.patch.patch_text} />
    </div>
  );
}

function PatchPane({ title, text }: { title: string; text: string }) {
  return (
    <div className="flex flex-col gap-1.5 rounded-md border border-base-300 bg-base-100 p-3">
      <span className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
        {title}
      </span>
      <pre className="m-0 max-h-[40vh] overflow-auto whitespace-pre-wrap break-words text-xs leading-relaxed text-base-content/85">
        {text || "(пусто)"}
      </pre>
    </div>
  );
}
