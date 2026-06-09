import hljs from "highlight.js/lib/common";

/**
 * Подсветка строки diff. highlight.js работает построчно: межстрочный контекст
 * (многострочные строки/комментарии) теряется, но для ревью это приемлемо и не
 * тянет тяжёлый stateful-рендер. Язык определяем по расширению; неизвестный —
 * отдаём экранированный plain text, чтобы фон add/remove строки всё равно был.
 */

const EXT_LANG: Record<string, string> = {
  ts: "typescript",
  tsx: "typescript",
  mts: "typescript",
  cts: "typescript",
  js: "javascript",
  jsx: "javascript",
  mjs: "javascript",
  cjs: "javascript",
  py: "python",
  rb: "ruby",
  rs: "rust",
  go: "go",
  java: "java",
  kt: "kotlin",
  swift: "swift",
  cs: "csharp",
  cpp: "cpp",
  cc: "cpp",
  c: "c",
  h: "cpp",
  css: "css",
  scss: "scss",
  less: "less",
  html: "xml",
  xml: "xml",
  vue: "xml",
  json: "json",
  yaml: "yaml",
  yml: "yaml",
  md: "markdown",
  sql: "sql",
  sh: "bash",
  bash: "bash",
  zsh: "bash",
  toml: "ini",
  ini: "ini",
  lua: "lua",
  php: "php"
};

export function detectLanguage(path: string): string | null {
  const ext = path.split(".").pop()?.toLowerCase() ?? "";
  // Незарегистрированный в common-бандле язык отсеется в highlightLine через
  // try/catch — отдельная проверка getLanguage не нужна.
  return EXT_LANG[ext] ?? null;
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

export function highlightLine(
  content: string,
  language: string | null
): string {
  if (content.length === 0) return "";
  if (language === null) return escapeHtml(content);
  try {
    return hljs.highlight(content, { language, ignoreIllegals: true }).value;
  } catch {
    return escapeHtml(content);
  }
}
