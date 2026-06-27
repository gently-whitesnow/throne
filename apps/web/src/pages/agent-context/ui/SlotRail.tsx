import { ArrowRight, Bot } from "lucide-react";
import { Fragment } from "react";

export interface SlotRailItem {
  id: string;
  index: number;
  label: string;
  badge?: number;
}

interface SlotRailProps {
  items: SlotRailItem[];
}

/**
 * Compact «assembly» strip: the four slots flow left-to-right into the agent, so
 * a newcomer sees the whole picture before scrolling. Each chip jumps to its
 * slot, giving orientation on a long single-screen page.
 */
export function SlotRail({ items }: SlotRailProps) {
  return (
    <nav
      aria-label="Слоты состава агента"
      className="flex flex-wrap items-center gap-x-1 gap-y-2 rounded-lg border border-base-300 bg-base-200/40 px-3 py-2"
    >
      {items.map((item) => (
        <Fragment key={item.id}>
          <a
            href={`#${item.id}`}
            className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs font-medium text-base-content/70 transition-[color,background-color,scale] hover:bg-base-100 hover:text-base-content active:scale-[0.96]"
          >
            <span className="font-bold tabular-nums text-primary">
              {item.index}
            </span>
            <span>{item.label}</span>
            {item.badge && item.badge > 0 ? (
              <span className="inline-flex h-[16px] min-w-[16px] items-center justify-center rounded-full bg-warning px-1 text-[10px] font-bold leading-none tabular-nums text-warning-content">
                {item.badge}
              </span>
            ) : null}
          </a>
          <ArrowRight
            aria-hidden
            size={13}
            strokeWidth={2}
            className="text-base-content/30"
          />
        </Fragment>
      ))}
      <span className="inline-flex items-center gap-1.5 rounded-md bg-primary/10 px-2 py-1 text-xs font-semibold text-primary">
        <Bot aria-hidden size={14} strokeWidth={2} />
        агент
      </span>
    </nav>
  );
}
