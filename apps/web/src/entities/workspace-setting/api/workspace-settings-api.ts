import { httpGet, settingsEndpoints } from "@/shared/api";

import type { WorkspaceSettings } from "../model/types";

export function fetchWorkspaceSettings(
  signal?: AbortSignal
): Promise<WorkspaceSettings> {
  return httpGet<WorkspaceSettings>(
    settingsEndpoints.getWorkspaceSettings(),
    signal
  );
}
