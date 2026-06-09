import { describe, expect, it } from "vitest";

import type { PullRequestDiffFile } from "@/entities/review-workspace";

import { buildFileTree, collectDirPaths } from "./build-file-tree";

function file(path: string): PullRequestDiffFile {
  return { path, status: "modified", patch: "" };
}

describe("buildFileTree", () => {
  it("группирует файлы по директориям, директории сверху, файлы снизу", () => {
    const tree = buildFileTree([
      file("src/app.ts"),
      file("src/lib/util.ts"),
      file("readme.md")
    ]);

    expect(
      tree.map((n) => (n.kind === "dir" ? `dir:${n.name}` : n.name))
    ).toEqual(["dir:src", "readme.md"]);
    const src = tree[0];
    if (src.kind !== "dir") throw new Error("ожидали директорию src");
    expect(src.children.map((n) => n.name)).toEqual(["lib", "app.ts"]);
  });

  it("схлопывает цепочку директорий с единственным потомком в один узел", () => {
    const tree = buildFileTree([file("apps/web/src/main.tsx")]);

    expect(tree).toHaveLength(1);
    const node = tree[0];
    if (node.kind !== "dir") throw new Error("ожидали директорию");
    expect(node.name).toBe("apps/web/src");
    expect(node.path).toBe("apps/web/src");
    expect(node.children.map((c) => c.name)).toEqual(["main.tsx"]);
  });

  it("collectDirPaths возвращает полные пути всех директорий", () => {
    const tree = buildFileTree([file("a/b/x.ts"), file("a/c/y.ts")]);
    expect(collectDirPaths(tree).sort()).toEqual(["a", "a/b", "a/c"]);
  });
});
