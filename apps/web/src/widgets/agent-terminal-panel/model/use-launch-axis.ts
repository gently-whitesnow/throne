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

import type {
  PersistedLaunchArgs,
  TerminalLaunchArgs,
  TerminalRunMode
} from "./types";

export interface LaunchAxisParams {
  /**
   * Persisted launch axis of the intent (ADR-0041): the live session's real parameters when
   * `sessionLive`, otherwise the last-used choice to pre-fill from. Null until the probe settles
   * or when the intent was never launched.
   */
  sessionLaunch: PersistedLaunchArgs | null;
  /** Live session → the axis is read-only and mirrors `sessionLaunch`, not the local draft. */
  sessionLive: boolean;
  /** Prefill waits for this so it seeds from `sessionLaunch` instead of racing the probe. */
  ready: boolean;
}

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
 *
 * Предзаполнение происходит один раз, когда каталог загружен, настройки успели
 * settle, а проба сессии отстрелялась (`ready`): persisted launch интента (ADR-0041)
 * главнее, затем default_vendor, затем дефолт каталога; дальше выбор оператора
 * главнее серверных дефолтов.
 *
 * Пока сессия живая (`sessionLive`) ось — read-only зеркало `sessionLaunch`
 * (фактических параметров сессии), а не локального черновика: дропдауны всё равно
 * залочены, смена параметров — только через новый запуск/перезапуск.
 */
export function useLaunchAxis({
  sessionLaunch,
  sessionLive,
  ready
}: LaunchAxisParams): LaunchAxis {
  const [vendor, setVendor] = useState<TerminalAgentVendor | null>(null);
  const [model, setModelState] = useState<string | null>(null);
  const [effort, setEffortState] = useState<TerminalReasoningEffort | null>(
    null
  );

  const catalogQuery = useTerminalVendorCatalogQuery();
  const settingsQuery = useTerminalSettingsQuery();
  const catalog = catalogQuery.data;

  // Only selectable vendors reach the launch dropdown — opencode «в разработке»
  // (selectable=false) is shown in /settings but never offered for launch.
  const selectableVendors = useMemo(
    () => (catalog?.vendors ?? []).filter((v) => v.selectable),
    [catalog]
  );

  const initialized = useRef(false);
  useEffect(() => {
    if (initialized.current || catalog === undefined) return;
    if (!settingsQuery.isFetched || !ready) return;

    // Persisted intent launch wins over the global default_vendor; the vendor must still exist
    // in the catalog AND be selectable (non-selectable vendors like opencode «в разработке» are
    // shown in settings but never offered for launch), otherwise fall back to the default.
    const persistedMeta =
      sessionLaunch !== null
        ? findVendorMetadata(catalog, sessionLaunch.vendor)
        : undefined;
    const persistedVendor =
      persistedMeta?.selectable === true ? sessionLaunch?.vendor : undefined;
    const resolved =
      persistedVendor ??
      resolveDefaultVendor(catalog, settingsQuery.data?.default_vendor);
    if (resolved === undefined) return;
    const meta = findVendorMetadata(catalog, resolved);
    if (meta === undefined) return;

    initialized.current = true;
    setVendor(resolved);
    if (persistedVendor !== undefined && sessionLaunch !== null) {
      setModelState(sessionLaunch.model);
      setEffortState(sessionLaunch.effort ?? meta.default_effort ?? null);
    } else {
      setModelState(meta.default_model ?? null);
      setEffortState(meta.default_effort ?? null);
    }
  }, [
    catalog,
    ready,
    sessionLaunch,
    settingsQuery.isFetched,
    settingsQuery.data?.default_vendor
  ]);

  // While the session is live the controls show its real axis, not the local draft — they are
  // frozen anyway, but a restart with new parameters must be reflected here immediately.
  const overriding = sessionLive && sessionLaunch !== null;
  const effectiveVendor = overriding ? sessionLaunch.vendor : vendor;
  const effectiveModel = overriding ? sessionLaunch.model : model;
  const effectiveEffort = overriding ? (sessionLaunch.effort ?? null) : effort;

  const selectedMeta = useMemo(
    () =>
      effectiveVendor === null
        ? undefined
        : findVendorMetadata(catalog, effectiveVendor),
    [catalog, effectiveVendor]
  );

  const onVendorChange = useCallback(
    (next: TerminalAgentVendor) => {
      const meta = findVendorMetadata(catalog, next);
      setVendor(next);
      if (meta !== undefined) {
        setModelState(meta.default_model ?? null);
        setEffortState(meta.default_effort ?? null);
      }
    },
    [catalog]
  );

  const launchReady =
    effectiveVendor !== null &&
    effectiveModel !== null &&
    selectedMeta !== undefined;

  const launchArgs = useCallback(
    (mode: TerminalRunMode): TerminalLaunchArgs | null =>
      effectiveVendor === null || effectiveModel === null
        ? null
        : {
            mode,
            vendor: effectiveVendor,
            model: effectiveModel,
            effort: effectiveEffort
          },
    [effectiveVendor, effectiveModel, effectiveEffort]
  );

  return {
    vendors: selectableVendors,
    vendor: effectiveVendor,
    selectedMeta,
    model: effectiveModel,
    effort: effectiveEffort,
    setModel: setModelState,
    setEffort: setEffortState,
    onVendorChange,
    metadataLoading: catalogQuery.isLoading || effectiveVendor === null,
    metadataError: catalogQuery.isError,
    launchReady,
    launchArgs
  };
}
