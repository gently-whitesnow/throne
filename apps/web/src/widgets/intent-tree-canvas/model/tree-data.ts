import type { IntentListItem } from "@/entities/intent";

import type { LayoutBounds, LayoutPosition } from "./layout";

export interface TreeNode {
  id: string;
  parents: readonly string[];
  intent: IntentListItem;
}

export interface TreeModel {
  nodes: readonly TreeNode[];
  byId: ReadonlyMap<string, TreeNode>;
  positions: ReadonlyMap<string, LayoutPosition>;
  bounds: LayoutBounds;
}

export type TreeLoadState =
  | { kind: "loading" }
  | { kind: "ready"; model: TreeModel }
  | { kind: "error"; message: string };
