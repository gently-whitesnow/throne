import type { InstructionsComponents } from "@/shared/api";

export type InstructionListItem =
  InstructionsComponents["schemas"]["InstructionListItemDto"];

export type InstructionDetail =
  InstructionsComponents["schemas"]["InstructionDetailDto"];

export type InstructionKind = "common" | "interview" | "work" | "dream" | "fix";

export interface InstructionKindMeta {
  label: string;
  ink: string;
  surface: string;
}

export const instructionKindMeta: Record<string, InstructionKindMeta> = {
  common: {
    label: "Common",
    ink: "#1F9D88",
    surface: "#E7F5ED"
  },
  interview: {
    label: "Interview",
    ink: "#3C78F2",
    surface: "#E8F0FF"
  },
  work: {
    label: "Work",
    ink: "#A87900",
    surface: "#FFF3D6"
  },
  dream: {
    label: "Dream",
    ink: "#5C49C7",
    surface: "#EEE9FF"
  },
  fix: {
    label: "Fix",
    ink: "#CF4D4D",
    surface: "#FDEAEA"
  }
};

export function instructionKindLabel(kind: string): InstructionKindMeta {
  return (
    instructionKindMeta[kind] ?? {
      label: kind,
      ink: "#202531",
      surface: "#F6F7FB"
    }
  );
}
