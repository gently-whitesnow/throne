import { httpGet, instructionPatchesEndpoints } from "@/shared/api";

import type {
  InstructionPatchPage,
  InstructionPatchStatus,
  InstructionPatchTargetKind
} from "../model/types";

export interface ListInstructionPatchesQuery {
  status?: InstructionPatchStatus;
  targetKind?: InstructionPatchTargetKind;
  limit?: number;
  cursor?: string;
}

export function listInstructionPatches(
  query: ListInstructionPatchesQuery,
  signal?: AbortSignal
): Promise<InstructionPatchPage> {
  const params = new URLSearchParams();
  if (query.status) params.set("status", query.status);
  if (query.targetKind) params.set("target_kind", query.targetKind);
  if (typeof query.limit === "number") params.set("limit", String(query.limit));
  if (query.cursor) params.set("cursor", query.cursor);
  const qs = params.toString();
  const url = `${instructionPatchesEndpoints.listInstructionPatches()}${qs ? `?${qs}` : ""}`;
  return httpGet<InstructionPatchPage>(url, signal);
}
