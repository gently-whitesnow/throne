import { dreamsEndpoints, httpGet } from "@/shared/api";

import type { DreamSourcePage } from "../model/types";

export function listDreamSources(
  signal?: AbortSignal
): Promise<DreamSourcePage> {
  return httpGet<DreamSourcePage>(dreamsEndpoints.listDreamSources(), signal);
}
