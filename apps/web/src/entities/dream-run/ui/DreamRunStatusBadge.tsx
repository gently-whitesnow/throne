import type { DreamRun } from "../model/types";

interface Props {
  status: DreamRun["status"];
}

const META: Record<
  DreamRun["status"],
  { label: string; surface: string; ink: string }
> = {
  pending: { label: "Pending", ink: "#A87900", surface: "#FFF3D6" },
  closed: { label: "Closed", ink: "#4C5567", surface: "#F6F7FB" }
};

export function DreamRunStatusBadge({ status }: Props) {
  const meta = META[status];
  return (
    <span
      className="inline-flex h-[18px] items-center rounded-full px-2 text-[10px] font-bold uppercase tracking-wide"
      style={{ background: meta.surface, color: meta.ink }}
    >
      {meta.label}
    </span>
  );
}
