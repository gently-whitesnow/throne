import type { InstructionsComponents } from "@/shared/api";

export type SkillsTreeData = InstructionsComponents["schemas"]["SkillsTreeDto"];
export type SkillNode = InstructionsComponents["schemas"]["SkillNodeDto"];
export type BundleNode = InstructionsComponents["schemas"]["BundleNodeDto"];
export type BundleEntryNode =
  InstructionsComponents["schemas"]["BundleEntryNodeDto"];

export type SelectedNode =
  | { kind: "skill"; skill: SkillNode }
  | { kind: "bundle"; skill: SkillNode }
  | { kind: "entry"; skill: SkillNode; entry: BundleEntryNode };
