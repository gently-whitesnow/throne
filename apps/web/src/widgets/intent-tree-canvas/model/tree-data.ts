import type { IntentListItem } from "@/entities/intent";

import type { LayoutBounds, LayoutPosition } from "./layout";

/**
 * The intent fields a canvas card needs to render. Both live list items and
 * link-summary peers (resolved/done neighbours) project onto this shape, so the
 * canvas can render either without fabricating list-only fields.
 */
export type CanvasCardIntent = Pick<
  IntentListItem,
  | "id"
  | "status"
  | "current_version"
  | "tags"
  | "text_short"
  | "sort_key"
  | "pinned_in"
>;

export interface TreeNode {
  id: string;
  parents: readonly string[];
  intent: CanvasCardIntent;
  /** True for resolved (done) neighbours pulled in by the «show resolved» toggle. */
  resolved: boolean;
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
