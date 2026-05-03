import { type DreamProposalSeverity, severityMeta } from "../model/types";

interface Props {
  severity: DreamProposalSeverity;
}

export function DreamProposalSeverityBadge({ severity }: Props) {
  const meta = severityMeta[severity];
  return (
    <span
      className="inline-flex h-[18px] items-center rounded-full px-2 text-[10px] font-bold uppercase tracking-wide"
      style={{ background: meta.surface, color: meta.ink }}
    >
      {meta.label}
    </span>
  );
}
