import { CircleAlert, Inbox, Terminal } from "lucide-react";

import { intentStatusMeta } from "@/entities/intent";
import { INBOX_HELP_CONTEXT, TERMINAL_RUNNING_CONTEXT } from "@/shared/lib";

interface InboxWidgetProps {
  helpCount: number;
  terminalRunningCount: number;
  activeContext: string | null;
  onSelect: (key: string) => void;
}

export function InboxWidget({
  helpCount,
  terminalRunningCount,
  activeContext,
  onSelect
}: InboxWidgetProps) {
  const helpMeta = intentStatusMeta.awaiting_operator;
  // A live terminal is an agent actively working — reuse the purple `work` token so the
  // bucket reads as "work in progress" and stays in lock-step with the status colour.
  const terminalMeta = intentStatusMeta.work;
  return (
    <section
      aria-label="Inbox: intents, ждущие действия оператора"
      className="flex-shrink-0 border-b border-base-300 bg-base-200/40 px-3 py-2"
    >
      <h3 className="m-0 mb-1 flex items-center gap-1.5 px-1 text-[11px] font-bold uppercase tracking-wider text-base-content/60">
        <Inbox aria-hidden size={12} strokeWidth={2.5} />
        Inbox
      </h3>
      <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
        {helpCount > 0 ? (
          <li>
            <InboxRow
              label="Жду ответа"
              count={helpCount}
              ink={helpMeta.ink}
              surface={helpMeta.surface}
              icon={<CircleAlert aria-hidden size={14} strokeWidth={2} />}
              active={activeContext === INBOX_HELP_CONTEXT}
              onSelect={() => {
                onSelect(INBOX_HELP_CONTEXT);
              }}
            />
          </li>
        ) : null}
        {terminalRunningCount > 0 ? (
          <li>
            <InboxRow
              label="Терминал запущен"
              count={terminalRunningCount}
              ink={terminalMeta.ink}
              surface={terminalMeta.surface}
              icon={<Terminal aria-hidden size={14} strokeWidth={2} />}
              active={activeContext === TERMINAL_RUNNING_CONTEXT}
              onSelect={() => {
                onSelect(TERMINAL_RUNNING_CONTEXT);
              }}
            />
          </li>
        ) : null}
      </ul>
    </section>
  );
}

interface InboxRowProps {
  label: string;
  count: number;
  ink: string;
  surface: string;
  icon: React.ReactNode;
  active: boolean;
  onSelect: () => void;
}

function InboxRow({
  label,
  count,
  ink,
  surface,
  icon,
  active,
  onSelect
}: InboxRowProps) {
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-current={active ? "true" : undefined}
      className={[
        "flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-[13px] font-semibold transition-colors",
        active ? "ring-1 ring-primary" : "hover:bg-base-300/40"
      ].join(" ")}
      style={{ background: surface, color: ink }}
    >
      <span aria-hidden style={{ color: ink }}>
        {icon}
      </span>
      <span className="min-w-0 flex-1 truncate">{label}</span>
      <span className="tabular-nums text-[11px] opacity-70">
        {String(count)}
      </span>
    </button>
  );
}
