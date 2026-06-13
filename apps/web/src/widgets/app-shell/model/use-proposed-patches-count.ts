import { useProposedPromptPartPatchesCount } from "@/entities/prompt-part-patch";

/**
 * Sidebar counter for the /improvements badge — counts PromptPartPatches
 * currently in `proposed` status owned by the caller. Realtime fanout
 * (`prompt_part_patch.*`) инвалидирует ключ через `RealtimeQueryBridge`.
 */
export function useProposedPatchesCount(): number {
  const query = useProposedPromptPartPatchesCount();
  return query.data ?? 0;
}
