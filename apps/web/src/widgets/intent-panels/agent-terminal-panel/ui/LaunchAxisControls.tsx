import type { RepositoryBinding } from "@/entities/repository-binding";
import {
  EFFORT_LABEL,
  type TerminalAgentVendor,
  type TerminalReasoningEffort,
  type TerminalVendorMetadata
} from "@/entities/terminal-setting";

import type { ReactNode } from "react";

import { RUN_MODE_LABEL, type TerminalRunMode } from "../model/types";

import { ReviewTargetSelect } from "./ReviewTargetSelect";

interface LaunchAxisControlsProps {
  mode: TerminalRunMode;
  modes: readonly TerminalRunMode[];
  onModeChange: (mode: TerminalRunMode) => void;
  /** Список вендоров из backend-каталога; пуст, пока metadata грузится. */
  vendors: readonly TerminalVendorMetadata[];
  vendor: string;
  onVendorChange: (vendor: TerminalAgentVendor) => void;
  /** Модели выбранного вендора из metadata. */
  models: readonly string[];
  model: string;
  onModelChange: (model: string) => void;
  /** Уровни усилия выбранного вендора из metadata. */
  efforts: readonly TerminalReasoningEffort[];
  effort: TerminalReasoningEffort | "";
  onEffortChange: (effort: TerminalReasoningEffort) => void;
  /** Скрывает контрол усилия для вендора без оси reasoning effort. */
  supportsEffort: boolean;
  /** Метаданные ещё грузятся/не загрузились — dropdown'ы заморожены. */
  metadataLoading: boolean;
  metadataError: boolean;
  /** PR/MR-таргеты для режима review; селектор виден только при mode=review и 2+ таргетах. */
  reviewTargets: readonly RepositoryBinding[];
  reviewBindingValue: string;
  onReviewBindingChange: (bindingId: string) => void;
  reviewDisabled: boolean;
  /** Опциональный блок квоты выбранного вендора + кнопка «Обновить» под селекторами. */
  quotaSlot?: ReactNode;
}

/**
 * Ось запуска (режим + vendor/model/effort + PR/MR для review), которую оператор
 * задаёт в preflight-модалке перед запуском. Раньше жила в тулбаре интента;
 * вынесена в модалку рядом с кнопкой запуска (live-сессия показывает значения
 * read-only бейджами, см. {@link LaunchAxisBadges}).
 */
export function LaunchAxisControls({
  mode,
  modes,
  onModeChange,
  vendors,
  vendor,
  onVendorChange,
  models,
  model,
  onModelChange,
  efforts,
  effort,
  onEffortChange,
  supportsEffort,
  metadataLoading,
  metadataError,
  reviewTargets,
  reviewBindingValue,
  onReviewBindingChange,
  reviewDisabled,
  quotaSlot
}: LaunchAxisControlsProps) {
  const dropdownDisabled = metadataLoading || metadataError;
  const selectClass = "select select-xs w-auto";

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <select
          aria-label="Режим запуска агента"
          title="Режим запуска агента"
          data-testid="agent-terminal-mode"
          className={selectClass}
          value={mode}
          onChange={(event) => {
            onModeChange(event.target.value as TerminalRunMode);
          }}
        >
          {modes.map((m) => (
            <option key={m} value={m}>
              {RUN_MODE_LABEL[m]}
            </option>
          ))}
        </select>

        <select
          aria-label="Агент терминала"
          title="Агент терминала"
          data-testid="agent-terminal-vendor"
          className={selectClass}
          value={vendor}
          disabled={dropdownDisabled}
          onChange={(event) => {
            onVendorChange(event.target.value);
          }}
        >
          {vendor === "" ? <option value="" disabled hidden /> : null}
          {vendors.map((v) => (
            <option key={v.vendor} value={v.vendor}>
              {v.label}
            </option>
          ))}
        </select>

        <select
          aria-label="Модель агента"
          title="Модель агента"
          data-testid="agent-terminal-model"
          className={selectClass}
          value={model}
          disabled={dropdownDisabled}
          onChange={(event) => {
            onModelChange(event.target.value);
          }}
        >
          {model === "" ? <option value="" disabled hidden /> : null}
          {models.map((m) => (
            <option key={m} value={m}>
              {m}
            </option>
          ))}
        </select>

        {supportsEffort ? (
          <select
            aria-label="Уровень усилия (reasoning)"
            title="Уровень усилия (reasoning)"
            data-testid="agent-terminal-effort"
            className={selectClass}
            value={effort}
            disabled={dropdownDisabled}
            onChange={(event) => {
              onEffortChange(event.target.value as TerminalReasoningEffort);
            }}
          >
            {effort === "" ? <option value="" disabled hidden /> : null}
            {efforts.map((e) => (
              <option key={e} value={e}>
                {EFFORT_LABEL[e]}
              </option>
            ))}
          </select>
        ) : null}

        {metadataLoading ? (
          <span className="text-[11px] text-base-content/60">
            Загружаем список агентов…
          </span>
        ) : null}

        {mode === "review" ? (
          <ReviewTargetSelect
            targets={reviewTargets}
            value={reviewBindingValue}
            disabled={reviewDisabled}
            onChange={onReviewBindingChange}
          />
        ) : null}
      </div>
      {quotaSlot}
    </div>
  );
}
