import { AlertTriangle } from "lucide-react";

export function ReviewArtifactStaleBanner() {
  return (
    <div
      role="status"
      className="flex items-center gap-2 border-b border-base-300 px-4 py-2 text-[12px]"
      style={{
        backgroundColor: "var(--color-status-attention-surface)",
        color: "var(--color-status-attention-ink)"
      }}
    >
      <AlertTriangle
        aria-hidden
        size={14}
        strokeWidth={2}
        className="shrink-0"
      />
      <span>
        AI-рекомендация устарела: head PR ушёл вперёд. Перепрогни{" "}
        <code className="font-mono">review</code>, чтобы обновить артефакт.
      </span>
    </div>
  );
}
