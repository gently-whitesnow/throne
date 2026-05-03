import type { DreamComponents } from "@/shared/api";

export type DreamProposal = DreamComponents["schemas"]["DreamProposalDto"];
export type DreamProposalPreview =
  DreamComponents["schemas"]["DreamProposalPreviewDto"];
export type DreamProposalSeverity = DreamProposal["severity"];
export type DreamProposalDecision = DreamProposal["decision"];

export interface SeverityMeta {
  label: string;
  ink: string;
  surface: string;
}

export const severityMeta: Record<DreamProposalSeverity, SeverityMeta> = {
  high: { label: "High", ink: "#CF4D4D", surface: "#FDEAEA" },
  medium: { label: "Medium", ink: "#A87900", surface: "#FFF3D6" },
  low: { label: "Low", ink: "#4C5567", surface: "#F6F7FB" }
};

export interface DecisionMeta {
  label: string;
  ink: string;
  surface: string;
}

export const decisionMeta: Record<DreamProposalDecision, DecisionMeta> = {
  pending: { label: "Pending", ink: "#A87900", surface: "#FFF3D6" },
  applied: { label: "Applied", ink: "#1F8F5F", surface: "#E7F5ED" },
  skipped: { label: "Skipped", ink: "#4C5567", surface: "#F6F7FB" }
};
