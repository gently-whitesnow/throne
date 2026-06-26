import { useMemo } from "react";

import { useIntents } from "./intents-queries";

/**
 * Множество intent_id с live tmux-сессией терминала. Membership — server-derived
 * (читается из tmux через фильтр `terminal_running`), поэтому держим его отдельным
 * запросом, а не полем строки списка. Realtime-инвалидация списков
 * (`terminal.session_started/stopped` в RealtimeQueryBridge) обновляет и его.
 *
 * Живых сессий по природе немного, так что подтянуть полный набор дёшево.
 */
export function useRunningTerminalIds(): ReadonlySet<string> {
  const { data } = useIntents({ terminalRunning: true });
  return useMemo(() => new Set((data ?? []).map((i) => i.id)), [data]);
}
