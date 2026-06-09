import type { PullRequestDiffFile } from "@/entities/review-workspace";

export interface FileTreeLeaf {
  kind: "file";
  /** Сегмент имени для показа (basename). */
  name: string;
  file: PullRequestDiffFile;
}

export interface FileTreeDir {
  kind: "dir";
  /** Показываемый сегмент; для схлопнутой цепочки — "a/b/c". */
  name: string;
  /** Полный путь директории — стабильный ключ для collapse-состояния. */
  path: string;
  children: FileTreeNode[];
}

export type FileTreeNode = FileTreeDir | FileTreeLeaf;

interface MutableDir {
  dirs: Map<string, MutableDir>;
  files: PullRequestDiffFile[];
  path: string;
}

function emptyDir(path: string): MutableDir {
  return { dirs: new Map(), files: [], path };
}

/**
 * Строит дерево директорий из плоского списка файлов diff'а. Порядок файлов
 * внутри директории сохраняется как во входном списке (вызывающий уже
 * отсортировал их natural-порядком). Цепочки директорий с единственным
 * потомком-директорией схлопываются в один узел ("a/b/c"), как в GitHub.
 */
export function buildFileTree(files: PullRequestDiffFile[]): FileTreeNode[] {
  const root = emptyDir("");

  for (const file of files) {
    const segments = file.path.split("/");
    let cursor = root;
    for (let i = 0; i < segments.length - 1; i += 1) {
      const segment = segments[i];
      const childPath = cursor.path ? `${cursor.path}/${segment}` : segment;
      let next = cursor.dirs.get(segment);
      if (!next) {
        next = emptyDir(childPath);
        cursor.dirs.set(segment, next);
      }
      cursor = next;
    }
    cursor.files.push(file);
  }

  return toNodes(root);
}

function toNodes(dir: MutableDir): FileTreeNode[] {
  const dirNodes: FileTreeDir[] = [];
  for (const [name, child] of dir.dirs) {
    dirNodes.push(collapseChain(name, child));
  }
  const fileNodes: FileTreeLeaf[] = dir.files.map((file) => ({
    kind: "file",
    name: file.path.slice(file.path.lastIndexOf("/") + 1),
    file
  }));
  // Директории сверху, файлы снизу — привычный порядок файлового дерева.
  return [...dirNodes, ...fileNodes];
}

function collapseChain(name: string, dir: MutableDir): FileTreeDir {
  let displayName = name;
  let current = dir;
  // Схлопываем, пока у директории ровно один потомок и это директория.
  while (current.files.length === 0 && current.dirs.size === 1) {
    const [childName, child] = [...current.dirs][0];
    displayName = `${displayName}/${childName}`;
    current = child;
  }
  return {
    kind: "dir",
    name: displayName,
    path: current.path,
    children: toNodes(current)
  };
}

/** Все directory-пути дерева — для дефолтного раскрытия всех веток. */
export function collectDirPaths(nodes: FileTreeNode[]): string[] {
  const out: string[] = [];
  for (const node of nodes) {
    if (node.kind === "dir") {
      out.push(node.path);
      out.push(...collectDirPaths(node.children));
    }
  }
  return out;
}
