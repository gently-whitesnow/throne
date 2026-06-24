import { Play, Square } from "lucide-react";

import {
  EFFORT_LABEL,
  type TerminalAgentVendor,
  type TerminalReasoningEffort,
  type TerminalVendorMetadata
} from "@/entities/terminal-setting";
import { Button } from "@/shared/ui";

import { RUN_MODE_LABEL, type TerminalRunMode } from "../model/types";

interface RunControlsProps {
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
  /** Метаданные ещё грузятся — dropdown'ы заморожены. */
  metadataLoading: boolean;
  /** Метаданные не загрузились — dropdown'ы заморожены, рендерим причину выше. */
  metadataError: boolean;
  /** Открывает preflight-модалку для нового запуска. */
  onRun: () => void;
  onKill: () => void;
  /** True когда disabled-state триггерится pre-flight'ом или live-сессией. */
  runDisabled: boolean;
  runDisabledReason: string | null;
  /** True когда `tmux has-session` → true: dropdown замораживаются. */
  sessionLive: boolean;
  /** True пока POST /run в полёте. */
  isStarting: boolean;
  /** True пока POST /kill в полёте. */
  isStopping: boolean;
}

export function RunControls({
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
  onRun,
  onKill,
  runDisabled,
  runDisabledReason,
  sessionLive,
  isStarting,
  isStopping
}: RunControlsProps) {
  const dropdownDisabled = sessionLive || metadataLoading || metadataError;

  const selectClass = "select select-xs w-auto";

  return (
    <div className="flex flex-wrap items-center gap-2">
      <select
        aria-label="Режим запуска агента"
        title="Режим запуска агента"
        data-testid="agent-terminal-mode"
        className={selectClass}
        value={mode}
        disabled={sessionLive}
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

      {sessionLive ? (
        <Button
          data-testid="agent-terminal-kill"
          className="btn-error"
          icon={<Square aria-hidden size={14} strokeWidth={2} />}
          disabled={isStarting || isStopping}
          onClick={onKill}
        >
          {isStopping ? "Завершаем…" : "Завершить сессию"}
        </Button>
      ) : (
        <Button
          data-testid="agent-terminal-run"
          variant="primary"
          icon={<Play aria-hidden size={14} strokeWidth={2} />}
          disabled={runDisabled || isStarting}
          title={runDisabledReason ?? undefined}
          onClick={onRun}
        >
          {isStarting ? "Запускаем…" : "Запустить в терминале"}
        </Button>
      )}
    </div>
  );
}
