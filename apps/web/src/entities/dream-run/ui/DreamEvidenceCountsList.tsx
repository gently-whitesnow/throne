import type { DreamEvidenceCounts } from "../model/types";

interface Props {
  counts: DreamEvidenceCounts;
  className?: string;
}

const ROWS: { key: keyof DreamEvidenceCounts; label: string }[] = [
  { key: "reviews", label: "Reviews" },
  { key: "qa", label: "QA" },
  { key: "mcp_errors", label: "MCP errors" },
  { key: "verification_failures", label: "Verify failures" },
  { key: "manual_corrections", label: "Manual fixes" },
  { key: "accepted_outcomes", label: "Accepted" },
  { key: "skipped_proposals", label: "Skipped" }
];

export function DreamEvidenceCountsList({ counts, className }: Props) {
  const visible = ROWS.filter(({ key }) => counts[key] > 0);
  if (visible.length === 0) {
    return (
      <p className={`m-0 text-xs text-base-content/60 ${className ?? ""}`}>
        Нет накопленной обратной связи в окне.
      </p>
    );
  }
  return (
    <ul
      className={`m-0 grid list-none grid-cols-2 gap-x-4 gap-y-1 p-0 text-xs ${className ?? ""}`}
    >
      {visible.map(({ key, label }) => (
        <li key={key} className="flex items-center justify-between gap-2">
          <span className="text-base-content/70">{label}</span>
          <span className="font-mono font-semibold text-base-content">
            {String(counts[key])}
          </span>
        </li>
      ))}
    </ul>
  );
}
