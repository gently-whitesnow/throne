import { httpGet, terminalEndpoints } from "@/shared/api";

import type { TerminalVendorCatalog } from "../model/types";

export function fetchTerminalVendorCatalog(
  signal?: AbortSignal
): Promise<TerminalVendorCatalog> {
  return httpGet<TerminalVendorCatalog>(
    terminalEndpoints.listTerminalVendors(),
    signal
  );
}
