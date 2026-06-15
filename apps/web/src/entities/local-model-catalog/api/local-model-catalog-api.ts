import { httpGet, settingsEndpoints } from "@/shared/api";

import type { LocalModelCatalog } from "../model/types";

export function fetchLocalModelCatalog(
  signal?: AbortSignal
): Promise<LocalModelCatalog> {
  return httpGet<LocalModelCatalog>(
    settingsEndpoints.getLocalModelCatalog(),
    signal
  );
}
