import type { DreamComponents } from "@/shared/api";

export type DreamRun = DreamComponents["schemas"]["DreamRunDto"];
export type DreamRunDetail = DreamComponents["schemas"]["DreamRunDetailDto"];
export type DreamEvidenceCounts =
  DreamComponents["schemas"]["DreamEvidenceCountsDto"];
export type DreamOmittedCounts =
  DreamComponents["schemas"]["DreamOmittedCountsDto"];
export type DreamEvidenceRef =
  DreamComponents["schemas"]["DreamEvidenceRefDto"];
export type DreamPendingCount =
  DreamComponents["schemas"]["DreamPendingCountDto"];

export interface EvidenceKindMeta {
  label: string;
  ink: string;
  surface: string;
}

const evidenceKindMeta: Record<string, EvidenceKindMeta> = {
  review: { label: "Review", ink: "#A87900", surface: "#FFF3D6" },
  qa: { label: "QA", ink: "#3C78F2", surface: "#E8F0FF" },
  mcp_call: { label: "MCP", ink: "#5C49C7", surface: "#EEE9FF" },
  outcome: { label: "Outcome", ink: "#1F9D88", surface: "#E7F5ED" },
  verification: { label: "Verify", ink: "#274DC6", surface: "#E8F0FF" },
  manual_correction: {
    label: "Manual fix",
    ink: "#CF4D4D",
    surface: "#FDEAEA"
  }
};

export function evidenceKindLabel(kind: string): EvidenceKindMeta {
  return (
    evidenceKindMeta[kind] ?? {
      label: kind,
      ink: "#202531",
      surface: "#F6F7FB"
    }
  );
}

export function pendingProposalsCount(run: DreamRun): number {
  return run.proposals.filter((p) => p.decision === "pending").length;
}

export function isEmptyRun(run: DreamRun): boolean {
  return run.proposals.length === 0;
}
