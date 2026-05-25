import type { SettingsComponents } from "@/shared/api";

export type WorkspaceSettings =
  SettingsComponents["schemas"]["WorkspaceSettingsDto"];

export type WorkspaceStatus = SettingsComponents["schemas"]["WorkspaceStatus"];

const KIB = 1024;
const MIB = KIB * 1024;
const GIB = MIB * 1024;

/**
 * Compact human-readable size for the workspace card. Uses binary units to
 * match `du -h` / Finder, with one fractional digit above KiB to keep the
 * cell narrow.
 */
export function formatWorkspaceSize(bytes: number | undefined): string {
  if (bytes === undefined) return "—";
  if (bytes < KIB) return `${String(bytes)} B`;
  if (bytes < MIB) return `${(bytes / KIB).toFixed(1)} KiB`;
  if (bytes < GIB) return `${(bytes / MIB).toFixed(1)} MiB`;
  return `${(bytes / GIB).toFixed(1)} GiB`;
}

export function isWorkspaceCalculating(status: WorkspaceStatus): boolean {
  return status === "calculating";
}
