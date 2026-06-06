import { httpGet, httpPost, settingsEndpoints } from "@/shared/api";

import type {
  WorkspaceCleanRequest,
  WorkspaceCleanResult,
  WorkspaceSettings
} from "../model/types";

export function fetchWorkspaceSettings(
  signal?: AbortSignal
): Promise<WorkspaceSettings> {
  return httpGet<WorkspaceSettings>(
    settingsEndpoints.getWorkspaceSettings(),
    signal
  );
}

export function cleanWorkspace(
  request: WorkspaceCleanRequest,
  signal?: AbortSignal
): Promise<WorkspaceCleanResult> {
  return httpPost<WorkspaceCleanResult>(
    settingsEndpoints.cleanWorkspace(),
    request,
    signal
  );
}
