import type { ComponentPropsWithoutRef, ReactNode } from "react";
import ReactMarkdown from "react-markdown";
import rehypeSanitize from "rehype-sanitize";
import remarkGfm from "remark-gfm";

import { MermaidDiagram } from "./MermaidDiagram";

interface MarkdownViewProps {
  markdown: string;
  className?: string;
}

/**
 * Shared markdown surface: GFM + sanitisation, with fenced ```mermaid``` blocks
 * promoted to rendered diagrams. First md renderer in the app (intent text and
 * PR comments still use <pre>); kept in shared/ui so they can adopt it later.
 */
export function MarkdownView({ markdown, className }: MarkdownViewProps) {
  return (
    <div className={`md-view ${className ?? ""}`.trim()}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeSanitize]}
        components={{ code: CodeBlock }}
      >
        {markdown}
      </ReactMarkdown>
    </div>
  );
}

function CodeBlock({
  className,
  children,
  ...rest
}: ComponentPropsWithoutRef<"code">) {
  const language = /language-(\w+)/.exec(className ?? "")?.[1];
  if (language === "mermaid") {
    return <MermaidDiagram code={flattenText(children).replace(/\n$/, "")} />;
  }
  return (
    <code className={className} {...rest}>
      {children}
    </code>
  );
}

function flattenText(node: ReactNode): string {
  if (typeof node === "string") return node;
  if (Array.isArray(node)) return node.map(flattenText).join("");
  return "";
}
