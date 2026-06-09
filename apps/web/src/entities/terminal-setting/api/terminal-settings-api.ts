import { httpGet, httpPut, settingsEndpoints } from "@/shared/api";

import type { TerminalAgentVendor, TerminalSettings } from "../model/types";

export function fetchTerminalSettings(
  signal?: AbortSignal
): Promise<TerminalSettings> {
  return httpGet<TerminalSettings>(
    settingsEndpoints.getTerminalSettings(),
    signal
  );
}

export function setDefaultTerminalVendor(
  vendor: TerminalAgentVendor,
  signal?: AbortSignal
): Promise<TerminalSettings> {
  return httpPut<TerminalSettings>(
    settingsEndpoints.setTerminalSettings(),
    { default_vendor: vendor },
    signal
  );
}
