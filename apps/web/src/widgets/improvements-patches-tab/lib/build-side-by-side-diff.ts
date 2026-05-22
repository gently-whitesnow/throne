import { diffLines } from "diff";

export type DiffCell =
  | { kind: "context"; text: string }
  | { kind: "removed"; text: string }
  | { kind: "added"; text: string }
  | { kind: "empty" };

export interface DiffRow {
  left: DiffCell;
  right: DiffCell;
}

function splitLines(value: string): string[] {
  const stripped = value.endsWith("\n") ? value.slice(0, -1) : value;
  return stripped.split("\n");
}

export function buildSideBySideDiff(
  current: string,
  proposed: string
): DiffRow[] {
  const changes = diffLines(current, proposed);
  const rows: DiffRow[] = [];

  for (let i = 0; i < changes.length; i++) {
    const part = changes[i];

    if (part.removed) {
      const removedLines = splitLines(part.value);
      const next = changes[i + 1];
      if (i + 1 < changes.length && next.added) {
        const addedLines = splitLines(next.value);
        const max = Math.max(removedLines.length, addedLines.length);
        for (let k = 0; k < max; k++) {
          rows.push({
            left:
              k < removedLines.length
                ? { kind: "removed", text: removedLines[k] }
                : { kind: "empty" },
            right:
              k < addedLines.length
                ? { kind: "added", text: addedLines[k] }
                : { kind: "empty" }
          });
        }
        i += 1;
        continue;
      }
      for (const line of removedLines) {
        rows.push({
          left: { kind: "removed", text: line },
          right: { kind: "empty" }
        });
      }
      continue;
    }

    if (part.added) {
      for (const line of splitLines(part.value)) {
        rows.push({
          left: { kind: "empty" },
          right: { kind: "added", text: line }
        });
      }
      continue;
    }

    for (const line of splitLines(part.value)) {
      rows.push({
        left: { kind: "context", text: line },
        right: { kind: "context", text: line }
      });
    }
  }

  return rows;
}
