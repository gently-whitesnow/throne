/**
 * Парсер сырого unified-diff `patch` из provider-derived diff (Slice 4A).
 *
 * Provider отдаёт patch без file-заголовков — только hunks (`@@ ... @@` + строки
 * с префиксом ` `/`+`/`-`). Парсер восстанавливает номера старой/новой строки,
 * чтобы viewer мог нарисовать колонки номеров и якорить inline-комментарий к
 * паре (side, line) ровно так, как этого ждёт submit-контракт.
 */

export type DiffRowKind = "context" | "add" | "del";

export interface DiffRow {
  kind: DiffRowKind;
  /** 1-based номер в старом файле; null для добавленной строки. */
  oldLine: number | null;
  /** 1-based номер в новом файле; null для удалённой строки. */
  newLine: number | null;
  /** Содержимое строки без diff-префикса. */
  content: string;
}

export interface DiffHunk {
  /** Сырая строка `@@ -a,b +c,d @@ section`. */
  header: string;
  oldStart: number;
  oldLines: number;
  newStart: number;
  newLines: number;
  rows: DiffRow[];
}

export interface ChangeCounts {
  additions: number;
  deletions: number;
}

const HUNK_HEADER = /^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@/;

export function parseUnifiedDiff(patch: string): DiffHunk[] {
  if (patch.length === 0) return [];

  const hunks: DiffHunk[] = [];
  let current: DiffHunk | null = null;
  let oldLine = 0;
  let newLine = 0;

  for (const raw of patch.split("\n")) {
    const headerMatch = HUNK_HEADER.exec(raw);
    if (headerMatch !== null) {
      current = {
        header: raw,
        oldStart: Number(headerMatch[1]),
        oldLines: Number(headerMatch[2] || "1"),
        newStart: Number(headerMatch[3]),
        newLines: Number(headerMatch[4] || "1"),
        rows: []
      };
      hunks.push(current);
      oldLine = Number(headerMatch[1]);
      newLine = Number(headerMatch[3]);
      continue;
    }

    if (current === null) continue;
    // «\ No newline at end of file» — служебный маркер, не строка кода.
    if (raw.startsWith("\\")) continue;
    // Пустой хвост от split («patch» оканчивается на \n) — не строка diff.
    if (raw.length === 0) continue;

    const marker = raw[0];
    const content = raw.slice(1);

    if (marker === "+") {
      current.rows.push({
        kind: "add",
        oldLine: null,
        newLine,
        content
      });
      newLine += 1;
    } else if (marker === "-") {
      current.rows.push({
        kind: "del",
        oldLine,
        newLine: null,
        content
      });
      oldLine += 1;
    } else {
      current.rows.push({
        kind: "context",
        oldLine,
        newLine,
        content
      });
      oldLine += 1;
      newLine += 1;
    }
  }

  return hunks;
}

/**
 * Лёгкий подсчёт +/- по patch без построения строк — для счётчиков в files rail,
 * где парсить каждый файл целиком дорого на крупных PR/MR.
 */
export function countPatchChanges(patch: string): ChangeCounts {
  let additions = 0;
  let deletions = 0;
  for (const raw of patch.split("\n")) {
    if (raw.length === 0 || raw.startsWith("\\")) continue;
    if (raw.startsWith("@@")) continue;
    if (raw.startsWith("+")) additions += 1;
    else if (raw.startsWith("-")) deletions += 1;
  }
  return { additions, deletions };
}
