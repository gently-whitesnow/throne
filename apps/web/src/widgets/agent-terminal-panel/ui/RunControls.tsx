import { Play, RotateCcw, Square } from "lucide-react";

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
  vendor: TerminalAgentVendor | "";
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
  /** Открывает preflight-модалку для перезапуска живой сессии. */
  onRestart: () => void;
  onKill: () => void;
  /** True когда disabled-state триггерится pre-flight'ом или live-сессией. */
  runDisabled: boolean;
  runDisabledReason: string | null;
  /** True когда `tmux has-session` → true: dropdown замораживаются. */
  sessionLive: boolean;
  /** True пока POST /run или /restart в полёте. */
  isStarting: boolean;
  /** True пока POST /kill в полёте. */
  isStopping: boolean;
  /** Включать UI Run-кнопки и xterm-блока (terminal-capability). */
  terminalEnabled: boolean;
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
  onRestart,
  onKill,
  runDisabled,
  runDisabledReason,
  sessionLive,
  isStarting,
  isStopping,
  terminalEnabled
}: RunControlsProps) {
  const dropdownDisabled = sessionLive || metadataLoading || metadataError;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <label className="flex items-center gap-2 text-xs text-base-content/70">
        <span>Режим</span>
        <select
          aria-label="Режим запуска агента"
          data-testid="agent-terminal-mode"
          className="select select-sm select-bordered"
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
      </label>

      <label className="flex items-center gap-2 text-xs text-base-content/70">
        <span>Агент</span>
        <select
          aria-label="Агент терминала"
          data-testid="agent-terminal-vendor"
          className="select select-sm select-bordered"
          value={vendor}
          disabled={dropdownDisabled}
          onChange={(event) => {
            onVendorChange(event.target.value as TerminalAgentVendor);
          }}
        >
          {vendor === "" ? <option value="" disabled hidden /> : null}
          {vendors.map((v) => (
            <option key={v.vendor} value={v.vendor}>
              {v.label}
            </option>
          ))}
        </select>
      </label>

      <label className="flex items-center gap-2 text-xs text-base-content/70">
        <span>Модель</span>
        <select
          aria-label="Модель агента"
          data-testid="agent-terminal-model"
          className="select select-sm select-bordered"
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
      </label>

      {supportsEffort ? (
        <label className="flex items-center gap-2 text-xs text-base-content/70">
          <span>Усилие</span>
          <select
            aria-label="Уровень усилия (reasoning)"
            data-testid="agent-terminal-effort"
            className="select select-sm select-bordered"
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
        </label>
      ) : null}

      {metadataLoading ? (
        <span className="text-[11px] text-base-content/60">
          Загружаем список агентов…
        </span>
      ) : null}

      {terminalEnabled ? (
        sessionLive ? (
          <>
            <Button
              data-testid="agent-terminal-restart"
              variant="primary"
              icon={<RotateCcw aria-hidden size={14} strokeWidth={2} />}
              disabled={isStarting || isStopping}
              onClick={onRestart}
            >
              {isStarting ? "Перезапускаем…" : "Перезапустить сессию"}
            </Button>
            <Button
              data-testid="agent-terminal-kill"
              className="btn-error"
              icon={<Square aria-hidden size={14} strokeWidth={2} />}
              disabled={isStarting || isStopping}
              onClick={onKill}
            >
              {isStopping ? "Завершаем…" : "Завершить сессию"}
            </Button>
          </>
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
        )
      ) : null}
    </div>
  );
}
