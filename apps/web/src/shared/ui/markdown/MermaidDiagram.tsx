import { useEffect, useId, useState } from "react";

import mermaid from "mermaid";

/** Mermaid keeps a single global config; flip theme to match the active shell. */
function activeTheme(): "dark" | "default" {
  if (typeof document === "undefined") return "default";
  return document.documentElement.getAttribute("data-theme") === "throne-dark"
    ? "dark"
    : "default";
}

interface MermaidDiagramProps {
  code: string;
}

/**
 * Renders a single `erDiagram` / mermaid block to inline SVG. `securityLevel:
 * "strict"` is load-bearing — the document body is user/agent-authored markdown,
 * so we never let mermaid emit click handlers or foreign HTML.
 */
export function MermaidDiagram({ code }: MermaidDiagramProps) {
  const renderId = `mermaid-${useId().replace(/[^a-zA-Z0-9-]/g, "")}`;
  const [svg, setSvg] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    mermaid.initialize({
      startOnLoad: false,
      securityLevel: "strict",
      theme: activeTheme(),
      fontFamily: '"Mona Sans", ui-sans-serif, system-ui, sans-serif'
    });
    mermaid
      .render(renderId, code)
      .then(({ svg: rendered }) => {
        if (cancelled) return;
        setSvg(rendered);
        setFailed(false);
      })
      .catch(() => {
        if (cancelled) return;
        setSvg(null);
        setFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, [code, renderId]);

  if (failed) {
    return (
      <div
        role="alert"
        className="my-2 rounded-md border border-error/30 bg-error/10 p-3 text-sm text-error"
      >
        <p className="m-0 mb-2 font-medium">Не удалось отрисовать диаграмму.</p>
        <pre className="m-0 overflow-x-auto whitespace-pre-wrap break-words font-mono text-xs text-base-content/80">
          {code}
        </pre>
      </div>
    );
  }

  if (svg === null) {
    return (
      <div className="my-2 flex h-24 items-center justify-center rounded-md border border-base-300 bg-base-200 text-sm text-base-content/60">
        Рисуем диаграмму…
      </div>
    );
  }

  return (
    <div
      className="my-3 overflow-x-auto rounded-md border border-base-300 bg-base-100 p-3"
      // mermaid output is sanitised by securityLevel: "strict"
      dangerouslySetInnerHTML={{ __html: svg }}
    />
  );
}
