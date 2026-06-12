import { httpPut, promptPartsEndpoints } from "@/shared/api";

import type { PromptPart, PromptPartModeRole } from "../model/types";

export function setPromptPartRoles(
  id: string,
  modeRoles: PromptPartModeRole[],
  signal?: AbortSignal
): Promise<PromptPart> {
  return httpPut<PromptPart>(
    promptPartsEndpoints.setPromptPartRoles(id),
    { mode_roles: modeRoles },
    signal
  );
}
