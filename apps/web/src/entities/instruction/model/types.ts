import type { InstructionsComponents } from "@/shared/api";

export type InstructionListItem =
  InstructionsComponents["schemas"]["InstructionListItemDto"];

export type InstructionDetail =
  InstructionsComponents["schemas"]["InstructionDetailDto"];

export type InstructionKind =
  | "common"
  | "interview"
  | "work"
  | "new_project"
  | "dream"
  | "fix";

export interface InstructionKindMeta {
  label: string;
  ink: string;
  surface: string;
}

export const instructionKindMeta: Record<string, InstructionKindMeta> = {
  common: {
    label: "Common",
    ink: "#187574",
    surface: "#c3faf5"
  },
  interview: {
    label: "Interview",
    ink: "#600000",
    surface: "#ffc6c6"
  },
  work: {
    label: "Work",
    ink: "#746019",
    surface: "#ffe6cd"
  },
  new_project: {
    label: "New project",
    ink: "#2a41b6",
    surface: "#dbe4ff"
  },
  dream: {
    label: "Dream",
    ink: "#5a1a8c",
    surface: "#ecdcfb"
  },
  fix: {
    label: "Fix",
    ink: "#7a1c4d",
    surface: "#ffd3e3"
  }
};

export function instructionKindLabel(kind: string): InstructionKindMeta {
  return (
    instructionKindMeta[kind] ?? {
      label: kind,
      ink: "#1c1c1e",
      surface: "#eceff5"
    }
  );
}
