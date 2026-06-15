import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import {
  findVendorMetadata,
  resolveDefaultVendor,
  useTerminalSettingsQuery,
  useTerminalVendorCatalogQuery,
  type TerminalAgentVendor,
  type TerminalReasoningEffort,
  type TerminalVendorMetadata
} from "@/entities/terminal-setting";

import type { TerminalLaunchArgs, TerminalRunMode } from "./types";

export interface LaunchAxis {
  vendors: readonly TerminalVendorMetadata[];
  vendor: TerminalAgentVendor | null;
  selectedMeta: TerminalVendorMetadata | undefined;
  model: string | null;
  effort: TerminalReasoningEffort | null;
  setModel: (model: string) => void;
  setEffort: (effort: TerminalReasoningEffort) => void;
  onVendorChange: (vendor: TerminalAgentVendor) => void;
  /** Метаданные ещё грузятся (или вендор не предзаполнен). */
  metadataLoading: boolean;
  /** Каталог не загрузился. */
  metadataError: boolean;
  /** Ось готова — можно собирать payload запуска. */
  launchReady: boolean;
  /** Полная ось запуска для preflight; null, пока не готова. */
  launchArgs: (mode: TerminalRunMode) => TerminalLaunchArgs | null;
}

/**
 * Держит ось запуска (вендор/модель/усилие) и тянет её дефолты/списки из
 * backend-каталога (`GET /terminal/vendors`) — фронт catalog не хардкодит.
 * Предзаполнение происходит один раз, когда каталог загружен, а настройки
 * успели settle: persisted default_vendor главнее дефолта каталога; дальше
 * выбор оператора главнее серверных дефолтов.
 */
export function useLaunchAxis(): LaunchAxis {
  const [vendor, setVendor] = useState<TerminalAgentVendor | null>(null);
  const [model, setModelState] = useState<string | null>(null);
  const [effort, setEffortState] = useState<TerminalReasoningEffort | null>(
    null
  );

  const catalogQuery = useTerminalVendorCatalogQuery();
  const settingsQuery = useTerminalSettingsQuery();
  const catalog = catalogQuery.data;

  const selectedMeta = useMemo(
    () => (vendor === null ? undefined : findVendorMetadata(catalog, vendor)),
    [catalog, vendor]
  );

  const initialized = useRef(false);
  useEffect(() => {
    if (initialized.current || catalog === undefined) return;
    if (!settingsQuery.isFetched) return;
    const resolved = resolveDefaultVendor(
      catalog,
      settingsQuery.data?.default_vendor
    );
    if (resolved === undefined) return;
    const meta = findVendorMetadata(catalog, resolved);
    if (meta === undefined) return;
    initialized.current = true;
    setVendor(resolved);
    setModelState(meta.default_model);
    setEffortState(meta.default_effort ?? null);
  }, [catalog, settingsQuery.isFetched, settingsQuery.data?.default_vendor]);

  const onVendorChange = useCallback(
    (next: TerminalAgentVendor) => {
      const meta = findVendorMetadata(catalog, next);
      setVendor(next);
      if (meta !== undefined) {
        setModelState(meta.default_model);
        setEffortState(meta.default_effort ?? null);
      }
    },
    [catalog]
  );

  const launchReady =
    vendor !== null && model !== null && selectedMeta !== undefined;

  const launchArgs = useCallback(
    (mode: TerminalRunMode): TerminalLaunchArgs | null =>
      vendor === null || model === null
        ? null
        : { mode, vendor, model, effort },
    [vendor, model, effort]
  );

  return {
    vendors: catalog?.vendors ?? [],
    vendor,
    selectedMeta,
    model,
    effort,
    setModel: setModelState,
    setEffort: setEffortState,
    onVendorChange,
    metadataLoading: catalogQuery.isLoading || vendor === null,
    metadataError: catalogQuery.isError,
    launchReady,
    launchArgs
  };
}
