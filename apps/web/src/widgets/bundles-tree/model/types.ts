import type { InstructionsComponents } from "@/shared/api";

export type BundlesTreeData =
  InstructionsComponents["schemas"]["BundlesTreeDto"];
export type BundleNode = InstructionsComponents["schemas"]["BundleNodeDto"];
export type BundleEntryNode =
  InstructionsComponents["schemas"]["BundleEntryNodeDto"];

export type SelectedNode =
  | { kind: "bundle"; bundle: BundleNode }
  | { kind: "entry"; bundle: BundleNode; entry: BundleEntryNode };
