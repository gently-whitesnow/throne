import { TerminalSquare } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";

import { Button } from "@/shared/ui";

import type { TerminalSessionStartedAt } from "../model/use-terminal-session";

import { TerminalView } from "./TerminalView";

interface TerminalLiveViewersProps {
  intentId: string;
  sessionLive: boolean;
  startedAt: TerminalSessionStartedAt | null;
  isOpeningNative: boolean;
  /** Есть ли на хосте задетектённый нативный опенер — иначе кнопку хэндоффа не рисуем. */
  hasNativeProviders: boolean;
  openNative: () => Promise<void>;
  markSessionEnded: () => void;
}

/**
 * Live-вьюеры одной tmux-сессии: встроенный (xterm) открыт автоматически, пока
 * сессия live, плюс хэндофф во внешний терминал как fallback. «2 строго
 * одновременно» не делаем (resize-конфликт tmux) — уход наружу гасит встроенный
 * (StopPipe на бэке), а возврат переоткрывает WebSocket, который сам ставит
 * pipe-pane заново.
 */
export function TerminalLiveViewers({
  intentId,
  sessionLive,
  startedAt,
  isOpeningNative,
  hasNativeProviders,
  openNative,
  markSessionEnded
}: TerminalLiveViewersProps) {
  const [nativeActive, setNativeActive] = useState(false);
  const nativeSwitchRef = useRef(false);

  const handleOpenNative = useCallback(() => {
    nativeSwitchRef.current = true;
    setNativeActive(true);
    void openNative();
  }, [openNative]);

  const handleReturnEmbedded = useCallback(() => {
    // Снимаем «взвод» хэндоффа: после возврата любой close встроенного снова
    // значимый (иначе первый реальный обрыв проглотился бы как плановый).
    nativeSwitchRef.current = false;
    setNativeActive(false);
  }, []);

  useEffect(() => {
    if (!sessionLive) {
      setNativeActive(false);
    }
  }, [sessionLive]);

  const handleTerminalClosed = useCallback(() => {
    if (nativeSwitchRef.current) {
      nativeSwitchRef.current = false;
      return;
    }
    markSessionEnded();
  }, [markSessionEnded]);

  if (!sessionLive) return null;

  // Встроенный открыт, пока сессия live и не отдана наружу.
  const embeddedVisible = !nativeActive;

  return (
    <>
      {hasNativeProviders ? (
        <div className="flex flex-wrap items-center gap-2">
          <Button
            aria-label="Открыть нативный терминал"
            data-testid="agent-terminal-open-native"
            disabled={isOpeningNative || nativeActive}
            icon={<TerminalSquare aria-hidden size={14} strokeWidth={2} />}
            onClick={handleOpenNative}
          >
            {isOpeningNative ? "Открываем…" : "Нативный терминал"}
          </Button>
        </div>
      ) : null}

      {embeddedVisible && startedAt !== null ? (
        <TerminalView
          intentId={intentId}
          attempt={startedAt.attempt}
          onClosed={handleTerminalClosed}
        />
      ) : null}

      {nativeActive ? (
        <div
          data-testid="agent-terminal-native-active"
          className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-base-300 bg-base-200 px-3 py-2 text-[13px] text-base-content/70"
        >
          <span>Открыто во внешнем терминале.</span>
          <Button
            data-testid="agent-terminal-return-embedded"
            icon={<TerminalSquare aria-hidden size={14} strokeWidth={2} />}
            onClick={handleReturnEmbedded}
          >
            Вернуться во встроенный
          </Button>
        </div>
      ) : null}
    </>
  );
}
