import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import type { TerminalVendorCatalog } from "../model/types";
import { fetchTerminalVendorCatalog } from "./terminal-vendor-catalog-api";

export const terminalVendorCatalogQueryKeys = {
  all: ["terminal-vendor-catalog"] as const,
  current: () => [...terminalVendorCatalogQueryKeys.all, "current"] as const
};

export function useTerminalVendorCatalogQuery(): UseQueryResult<TerminalVendorCatalog> {
  return useQuery({
    queryKey: terminalVendorCatalogQueryKeys.current(),
    queryFn: ({ signal }) => fetchTerminalVendorCatalog(signal),
    // Каталог статичен на бэкенде: меняется только деплоем, держим долго.
    staleTime: 5 * 60_000
  });
}
