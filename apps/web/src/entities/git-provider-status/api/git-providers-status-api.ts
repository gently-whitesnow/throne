import {
  httpGet,
  httpPut,
  settingsEndpoints,
  type SettingsComponents
} from "@/shared/api";

import type { GitProvidersStatus } from "../model/types";

type GitLabHostSettings =
  SettingsComponents["schemas"]["GitLabHostSettingsDto"];

export function fetchGitProvidersStatus(
  signal?: AbortSignal
): Promise<GitProvidersStatus> {
  return httpGet<GitProvidersStatus>(
    settingsEndpoints.getGitProvidersStatus(),
    signal
  );
}

export function setGitLabHost(
  host: string,
  signal?: AbortSignal
): Promise<GitLabHostSettings> {
  return httpPut<GitLabHostSettings>(
    settingsEndpoints.setGitLabHost(),
    { host },
    signal
  );
}
