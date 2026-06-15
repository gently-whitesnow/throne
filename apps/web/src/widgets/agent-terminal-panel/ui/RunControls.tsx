import { Play, RotateCcw, Square } from "lucide-react";

import {
  EFFORT_LABEL,
  TERMINAL_EFFORTS,
  TERMINAL_VENDORS,
  VENDOR_LABEL,
  VENDOR_MODELS,
  type TerminalAgentVendor,
  type TerminalReasoningEffort
} from "@/entities/terminal-setting";
import { Button } from "@/shared/ui";

import {
  RUN_MODE_LABEL,
  TERMINAL_RUN_MODES,
  type TerminalRunMode
} from "../model/types";

interface RunControlsProps {
  mode: TerminalRunMode;
  onModeChange: (mode: TerminalRunMode) => void;
  vendor: TerminalAgentVendor;
  onVendorChange: (vendor: TerminalAgentVendor) => void;
  model: string;
  onModelChange: (model: string) => void;
  effort: TerminalReasoningEffort;
  onEffortChange: (effort: TerminalReasoningEffort) => void;
  /** Скрывает контрол усилия для вендора без оси reasoning effort. */
  supportsEffort: boolean;
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
  onModeChange,
  vendor,
  onVendorChange,
  model,
  onModelChange,
  effort,
  onEffortChange,
  supportsEffort,
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
  const dropdownDisabled = sessionLive;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <label className="flex items-center gap-2 text-xs text-base-content/70">
        <span>Режим</span>
        <select
          aria-label="Режим запуска агента"
          data-testid="agent-terminal-mode"
          className="select select-sm select-bordered"
          value={mode}
          disabled={dropdownDisabled}
          onChange={(event) => {
            onModeChange(event.target.value as TerminalRunMode);
          }}
        >
          {TERMINAL_RUN_MODES.map((m) => (
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
          {TERMINAL_VENDORS.map((v) => (
            <option key={v} value={v}>
              {VENDOR_LABEL[v]}
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
          {VENDOR_MODELS[vendor].map((m) => (
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
            {TERMINAL_EFFORTS.map((e) => (
              <option key={e} value={e}>
                {EFFORT_LABEL[e]}
              </option>
            ))}
          </select>
        </label>
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
