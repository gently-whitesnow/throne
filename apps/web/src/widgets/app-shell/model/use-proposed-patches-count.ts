import { useProposedInstructionPatchesCount } from "@/entities/instruction-patch";

/**
 * Sidebar counter for the /improvements badge — counts InstructionPatches
 * currently in `proposed` status owned by the caller. Realtime fanout
 * (`instruction_patch.*`) инвалидирует ключ через `RealtimeQueryBridge`.
 */
export function useProposedPatchesCount(): number {
  const query = useProposedInstructionPatchesCount();
  return query.data ?? 0;
}
