import { describe, expect, it } from "vitest";

import {
  fileLinesToContextRows,
  findDiffGaps,
  mergeFullFileLinesWithDiff
} from "./diff-context";
import { parseUnifiedDiff } from "./parse-unified-diff";

describe("findDiffGaps", () => {
  it("находит верхний, промежуточный и нижний пропуски с old/new дельтой", () => {
    const hunks = parseUnifiedDiff(
      ["@@ -3,2 +5,2 @@", " a", "+b", "@@ -12,1 +16,1 @@", " z"].join("\n")
    );

    expect(findDiffGaps(hunks, 20)).toEqual([
      { id: "top", from: 1, to: 4, oldLineDelta: -2 },
      { id: "between-0-1", from: 7, to: 15, oldLineDelta: -2 },
      { id: "bottom", from: 17, to: 20, oldLineDelta: -4 }
    ]);
  });

  it("для неизвестного total_lines оставляет нижний пропуск открытым", () => {
    const hunks = parseUnifiedDiff(["@@ -1,1 +1,1 @@", " a"].join("\n"));

    expect(findDiffGaps(hunks, null)).toContainEqual({
      id: "bottom",
      from: 2,
      to: null,
      oldLineDelta: 0
    });
  });

  it("не строит нижний пропуск с нулевой строкой для удалённого файла", () => {
    const hunks = parseUnifiedDiff(["@@ -1,1 +0,0 @@", "-gone"].join("\n"));

    expect(findDiffGaps(hunks, null)).toEqual([]);
  });
});

describe("fileLinesToContextRows", () => {
  it("вычисляет oldLine из дельты границы пропуска", () => {
    expect(fileLinesToContextRows([{ line: 10, content: "same" }], -3)).toEqual(
      [{ kind: "context", oldLine: 7, newLine: 10, content: "same" }]
    );
  });
});

describe("mergeFullFileLinesWithDiff", () => {
  it("рендерит полный head-файл и накладывает add/del строки из patch", () => {
    const hunks = parseUnifiedDiff(
      [
        "@@ -1,4 +1,5 @@",
        " one",
        "-two",
        "+two changed",
        "+three",
        " four"
      ].join("\n")
    );

    const rows = mergeFullFileLinesWithDiff(
      [
        { line: 1, content: "one" },
        { line: 2, content: "two changed" },
        { line: 3, content: "three" },
        { line: 4, content: "four" },
        { line: 5, content: "five" }
      ],
      hunks
    );

    expect(rows).toEqual([
      { kind: "context", oldLine: 1, newLine: 1, content: "one" },
      { kind: "del", oldLine: 2, newLine: null, content: "two" },
      { kind: "add", oldLine: null, newLine: 2, content: "two changed" },
      { kind: "add", oldLine: null, newLine: 3, content: "three" },
      { kind: "context", oldLine: 3, newLine: 4, content: "four" },
      { kind: "context", oldLine: 4, newLine: 5, content: "five" }
    ]);
  });
});
