import { describe, expect, it } from "vitest";

import { detectLanguage, highlightLine } from "./highlight-line";

describe("detectLanguage", () => {
  it("маппит расширение в язык highlight.js", () => {
    expect(detectLanguage("src/app.ts")).toBe("typescript");
    expect(detectLanguage("a/b/c.tsx")).toBe("typescript");
    expect(detectLanguage("main.go")).toBe("go");
  });

  it("возвращает null для неизвестного расширения", () => {
    expect(detectLanguage("LICENSE")).toBeNull();
    expect(detectLanguage("data.bin")).toBeNull();
  });
});

describe("highlightLine", () => {
  it("подсвечивает ключевые слова в токен-классы", () => {
    const html = highlightLine("const x = 1;", "typescript");
    expect(html).toContain("hljs-keyword");
  });

  it("экранирует html, когда язык не определён", () => {
    expect(highlightLine("<a> & </a>", null)).toBe(
      "&lt;a&gt; &amp; &lt;/a&gt;"
    );
  });

  it("пустую строку отдаёт пустой", () => {
    expect(highlightLine("", "typescript")).toBe("");
  });
});
