import { type DreamReadinessStatus, readinessStatusMeta } from "../model/types";

interface Props {
  status: DreamReadinessStatus;
}

export function DreamReadinessStatusBadge({ status }: Props) {
  const meta = readinessStatusMeta[status];
  return (
    <span
      className="inline-flex h-[20px] items-center rounded-full px-2.5 text-[11px] font-bold uppercase tracking-wide"
      style={{ background: meta.surface, color: meta.ink }}
    >
      {meta.label}
    </span>
  );
}
