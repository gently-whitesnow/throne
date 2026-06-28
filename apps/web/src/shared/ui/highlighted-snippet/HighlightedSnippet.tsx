import { Fragment } from "react";

// Highlight markers emitted by the search-core snippet (STX / ETX). They never occur in
// intent text, so we split on them and render the enclosed runs as <mark> — as text nodes,
// never raw markup, so a snippet can't inject HTML.
const MARK_OPEN = "\u0002";
const MARK_CLOSE = "\u0003";

interface HighlightedSnippetProps {
  snippet: string;
  className?: string;
}

export function HighlightedSnippet({
  snippet,
  className
}: HighlightedSnippetProps) {
  return <span className={className}>{toSegments(snippet)}</span>;
}

function toSegments(snippet: string) {
  return snippet.split(MARK_OPEN).map((chunk, openIndex) => {
    if (openIndex === 0) {
      return <Fragment key={openIndex}>{chunk}</Fragment>;
    }
    const [highlighted, ...rest] = chunk.split(MARK_CLOSE);
    return (
      <Fragment key={openIndex}>
        <mark className="bg-primary/20 text-inherit">{highlighted}</mark>
        {rest.join(MARK_CLOSE)}
      </Fragment>
    );
  });
}
