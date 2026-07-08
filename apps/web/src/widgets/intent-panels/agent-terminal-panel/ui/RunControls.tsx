import { Play, Square } from "lucide-react";

import { Button } from "@/shared/ui";

interface RunControlsProps {
  /** True когда `tmux has-session` → true: показываем завершение вместо запуска. */
  sessionLive: boolean;
  /** Открывает preflight-модалку для нового (холодного) запуска. */
  onRun: () => void;
  onKill: () => void;
  /** Live + интервью + «Жду ответа» → показываем прямую кнопку перехода в работу. */
  showStartWork: boolean;
  /** Открывает preflight-модалку с предустановленным режимом «Работа». */
  onStartWork: () => void;
  /** True когда холодный запуск гейтится pre-flight'ом (клоны/каталог). */
  runDisabled: boolean;
  runDisabledReason: string | null;
  /** True пока POST /run в полёте. */
  isStarting: boolean;
  /** True пока POST /kill в полёте. */
  isStopping: boolean;
}

/**
 * Ряд действий тулбара. До live-сессии — одна кнопка «Запустить в терминале»
 * (открывает модалку с осью запуска). В live-сессии — «Завершить сессию» и,
 * для интервью в ожидании оператора, прямой переход «Начать работу»
 * (kill текущей + запуск новой в режиме «Работа» за один клик).
 */
export function RunControls({
  sessionLive,
  onRun,
  onKill,
  showStartWork,
  onStartWork,
  runDisabled,
  runDisabledReason,
  isStarting,
  isStopping
}: RunControlsProps) {
  if (!sessionLive) {
    return (
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
    );
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Button
        data-testid="agent-terminal-kill"
        className="btn-error"
        icon={<Square aria-hidden size={14} strokeWidth={2} />}
        disabled={isStarting || isStopping}
        onClick={onKill}
      >
        {isStopping ? "Завершаем…" : "Завершить сессию"}
      </Button>
      {showStartWork ? (
        <Button
          data-testid="agent-terminal-start-work"
          variant="primary"
          icon={<Play aria-hidden size={14} strokeWidth={2} />}
          disabled={isStarting || isStopping}
          title="Перезапустить сессию в режиме «Работа»"
          onClick={onStartWork}
        >
          Начать работу
        </Button>
      ) : null}
    </div>
  );
}
