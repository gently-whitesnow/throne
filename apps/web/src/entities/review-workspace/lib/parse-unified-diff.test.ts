import { describe, expect, it } from "vitest";

import { countPatchChanges, parseUnifiedDiff } from "./parse-unified-diff";

const PATCH = [
  "@@ -1,4 +1,5 @@",
  " context-a",
  "-removed-b",
  "+added-b",
  "+added-c",
  " context-d",
  "\\ No newline at end of file"
].join("\n");

describe("parseUnifiedDiff", () => {
  it("восстанавливает номера строк старого и нового файла по hunk-заголовку", () => {
    const [hunk] = parseUnifiedDiff(PATCH);

    expect(hunk.header).toBe("@@ -1,4 +1,5 @@");
    expect(hunk).toMatchObject({
      oldStart: 1,
      oldLines: 4,
      newStart: 1,
      newLines: 5
    });
    expect(hunk.rows).toEqual([
      { kind: "context", oldLine: 1, newLine: 1, content: "context-a" },
      { kind: "del", oldLine: 2, newLine: null, content: "removed-b" },
      { kind: "add", oldLine: null, newLine: 2, content: "added-b" },
      { kind: "add", oldLine: null, newLine: 3, content: "added-c" },
      { kind: "context", oldLine: 3, newLine: 4, content: "context-d" }
    ]);
  });

  it("игнорирует служебный маркер «No newline» и не считает его строкой", () => {
    const [hunk] = parseUnifiedDiff(PATCH);
    expect(hunk.rows.some((r) => r.content.startsWith("No newline"))).toBe(
      false
    );
  });

  it("возвращает пустой список для бинарного/пустого patch", () => {
    expect(parseUnifiedDiff("")).toEqual([]);
  });

  it("раскладывает несколько hunk'ов независимо", () => {
    const multi = [
      "@@ -1,1 +1,1 @@",
      "-a",
      "+b",
      "@@ -10,1 +10,2 @@",
      " c",
      "+d"
    ].join("\n");
    const hunks = parseUnifiedDiff(multi);
    expect(hunks).toHaveLength(2);
    expect(hunks[1].rows[0]).toMatchObject({ oldLine: 10, newLine: 10 });
    expect(hunks[1].rows[1]).toMatchObject({ newLine: 11, oldLine: null });
  });

  it("читает нулевую длину из hunk-заголовка удалённого файла", () => {
    const [hunk] = parseUnifiedDiff(["@@ -1,2 +0,0 @@", "-a", "-b"].join("\n"));

    expect(hunk).toMatchObject({
      oldStart: 1,
      oldLines: 2,
      newStart: 0,
      newLines: 0
    });
  });
});

describe("countPatchChanges", () => {
  it("считает добавленные и удалённые строки без учёта hunk-заголовков", () => {
    expect(countPatchChanges(PATCH)).toEqual({ additions: 2, deletions: 1 });
  });

  it("для пустого patch возвращает нули", () => {
    expect(countPatchChanges("")).toEqual({ additions: 0, deletions: 0 });
  });
});
