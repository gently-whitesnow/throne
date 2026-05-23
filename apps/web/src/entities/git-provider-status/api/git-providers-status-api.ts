import { httpGet, settingsEndpoints } from "@/shared/api";

import type { GitProvidersStatus } from "../model/types";

export function fetchGitProvidersStatus(
  signal?: AbortSignal
): Promise<GitProvidersStatus> {
  return httpGet<GitProvidersStatus>(
    settingsEndpoints.getGitProvidersStatus(),
    signal
  );
}
