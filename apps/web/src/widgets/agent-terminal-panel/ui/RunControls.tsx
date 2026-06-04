import { Check, Copy, Play, RotateCcw } from "lucide-react";
import { useState } from "react";

import { Button } from "@/shared/ui";

import {
  RUN_MODE_LABEL,
  TERMINAL_RUN_MODES,
  type TerminalRunMode
} from "../model/types";

interface RunControlsProps {
  mode: TerminalRunMode;
  onModeChange: (mode: TerminalRunMode) => void;
  promptText: string;
  onRun: () => void;
  onRestart: () => void;
  /** True когда disabled-state триггерится pre-flight'ом или live-сессией. */
  runDisabled: boolean;
  runDisabledReason: string | null;
  /** True когда `tmux has-session` → true: dropdown и Copy prompt замораживаются. */
  sessionLive: boolean;
  /** True пока POST /run или /restart в полёте. */
  isStarting: boolean;
  /** Включать UI Run-кнопки и xterm-блока (terminal-capability). */
  terminalEnabled: boolean;
}

export function RunControls({
  mode,
  onModeChange,
  promptText,
  onRun,
  onRestart,
  runDisabled,
  runDisabledReason,
  sessionLive,
  isStarting,
  terminalEnabled
}: RunControlsProps) {
  const [copied, setCopied] = useState(false);

  function handleCopyPrompt() {
    void (async () => {
      try {
        await navigator.clipboard.writeText(promptText);
        setCopied(true);
        window.setTimeout(() => {
          setCopied(false);
        }, 1500);
      } catch {
        setCopied(false);
      }
    })();
  }

  const dropdownDisabled = sessionLive;
  const copyDisabled = sessionLive;

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

      <Button
        data-testid="agent-terminal-copy"
        aria-label={
          copied
            ? "Промпт скопирован"
            : "Скопировать промпт для запуска агента вручную"
        }
        icon={
          copied ? (
            <Check aria-hidden size={14} strokeWidth={2} />
          ) : (
            <Copy aria-hidden size={14} strokeWidth={2} />
          )
        }
        disabled={copyDisabled}
        onClick={handleCopyPrompt}
      >
        {copied ? "Скопировано" : "Copy prompt"}
      </Button>

      {terminalEnabled ? (
        sessionLive ? (
          <Button
            data-testid="agent-terminal-restart"
            variant="primary"
            icon={<RotateCcw aria-hidden size={14} strokeWidth={2} />}
            disabled={isStarting}
            onClick={onRestart}
          >
            {isStarting ? "Перезапускаем…" : "Перезапустить сессию"}
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
        )
      ) : null}
    </div>
  );
}
