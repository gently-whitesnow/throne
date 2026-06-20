import { httpGet, httpPut, settingsEndpoints } from "@/shared/api";

import type {
  SkillModeDefaults,
  UpdateSkillModeDefaultsRequest
} from "../model/types";

export function fetchSkillModeDefaults(
  signal?: AbortSignal
): Promise<SkillModeDefaults> {
  return httpGet<SkillModeDefaults>(
    settingsEndpoints.getSkillModeDefaults(),
    signal
  );
}

export function setSkillModeDefaults(
  request: UpdateSkillModeDefaultsRequest,
  signal?: AbortSignal
): Promise<SkillModeDefaults> {
  return httpPut<SkillModeDefaults>(
    settingsEndpoints.setSkillModeDefaults(),
    request,
    signal
  );
}
