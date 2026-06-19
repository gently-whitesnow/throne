import type {
  ReviewFileOrderEntry,
  ReviewFileRisk
} from "@/entities/pull-request-artifact";
import type { PullRequestDiffFile } from "@/entities/review-workspace";

export interface FileOrderHint {
  reason: string | null;
  risk: ReviewFileRisk | null;
}

export interface OrderedFiles {
  files: PullRequestDiffFile[];
  /** Per-path hints (reason + risk) for files that the artifact ranked. */
  hints: Map<string, FileOrderHint>;
}

/**
 * Reorders PR diff files using AI `file_order` from a `review_recommendation`
 * artifact. Ranked files come first in artifact order; remaining PR files keep
 * the caller-provided natural order. Hints map covers only ranked paths.
 */
export function orderFilesByAi(
  natural: PullRequestDiffFile[],
  fileOrder: ReviewFileOrderEntry[] | undefined | null
): OrderedFiles {
  const hints = new Map<string, FileOrderHint>();
  if (fileOrder === undefined || fileOrder === null || fileOrder.length === 0) {
    return { files: natural, hints };
  }

  const byPath = new Map(natural.map((f) => [f.path, f] as const));
  const seen = new Set<string>();
  const ranked: PullRequestDiffFile[] = [];

  for (const entry of fileOrder) {
    const file = byPath.get(entry.path);
    if (file === undefined || seen.has(entry.path)) continue;
    seen.add(entry.path);
    ranked.push(file);
    hints.set(entry.path, {
      reason: entry.reason ?? null,
      risk: entry.risk ?? null
    });
  }

  const rest = natural.filter((f) => !seen.has(f.path));
  return { files: [...ranked, ...rest], hints };
}
