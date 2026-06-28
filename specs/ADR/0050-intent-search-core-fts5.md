# ADR-0050: Ranked intent search-core on SQLite FTS5

## Status

Accepted
Date: 2026-06-28

## Context

Linking intents went through the same list endpoint the rest of the app uses
(`GET /api/v1/intents?query=`). The query path filtered `Intent.text` with an AND of
per-token `LIKE %word%` predicates: a full table scan, no ranking, no prefix matching, no
relevance. The operator's pain in the «New link» autocomplete was that the right intent
sank among irrelevant substring hits — the result set was unordered.

A point-fix on the autocomplete would have to be redone the moment a second consumer (a
global Cmd-K search) appears. The storage is already SQLite (ADR-0047), which ships FTS5 —
tokenization, prefix queries and BM25 ranking with no new infrastructure or dependency.

## Subdomain classification

Generic sticky: search is a generic capability; FTS5/BM25 is the chosen local-first
implementation. Semantic / embedding search is explicitly out of scope and, if ever
needed, layers over this core rather than replacing it.

## Volatility check

Accidental volatility contained: the engine sits behind a port (`IIntentSearchReader`)
that knows nothing about callers or UI, so swapping the ranking implementation or adding a
second consumer does not ripple outward.

## Decision

1. **Engine.** A standalone FTS5 virtual table `intents_fts(intent_id UNINDEXED, title,
   text)` with the `unicode61 remove_diacritics 2` tokenizer. Search ranks by
   `bm25(intents_fts, 1.0, 5.0, 1.0)` — title weighted above body so a title hit outranks a
   body-only hit — and returns the highlighted body excerpt via `snippet()`.
2. **Index maintenance via triggers.** `AFTER INSERT / DELETE / UPDATE OF title, text`
   triggers on `intents` keep `intents_fts` in lock-step with every write path
   (create / replace / insert / status-append / title edit / delete) without touching
   repository code. The virtual table and triggers are raw SQL in an EF migration; EF
   cannot model FTS5, so they are intentionally absent from the model snapshot. The bundled
   `e_sqlite3` runs with `trusted_schema=ON`, so FTS5 triggers create and fire normally.
3. **Layering.** The search-core is the Application port `IIntentSearchReader` (raw text →
   ranked `(intent_id, snippet)`), implemented in Infrastructure as `EfIntentSearchReader`.
   It is deliberately ignorant of structural filters. The intent reader is the first
   consumer: on the `query` path it ranks via the core, then applies the existing
   structural filters (status / tag / untagged / pinned) over the ranked candidate set and
   returns the page in rank order. A future global search consumes the same port directly.
4. **Query semantics.** User input is tokenized on whitespace; each token becomes a quoted
   prefix term (`"token"*`) and terms are ANDed. Quoting neutralizes FTS5 syntax
   characters; tokens with no letter/digit are dropped; an all-punctuation query yields an
   empty page rather than a syntax error.
5. **Wire shape.** `IntentListItemDto.snippet` carries the highlighted excerpt with matched
   runs wrapped in STX/ETX control-character markers — present only on the ranked `query`
   path. Clients split on the markers and render the runs as text (never raw markup). The
   ranked `query` path is single-page (`next_cursor` is null); the autocomplete reads the
   first page only.

## Consequences

### Positive

- The autocomplete returns relevance-ranked, prefix-aware results with highlighted snippets
  and title-first ordering; the right intent surfaces at the top.
- One reusable engine: a future global search reuses `IIntentSearchReader` unchanged.
- Triggers make the index self-maintaining — no write path can forget to reindex.

### Negative / Risks

- The ranked path does not paginate beyond the first page; deep paged search would need a
  rank-stable cursor.
- FTS5 duplicates `title`/`text` into the index (standalone, not external-content); storage
  cost is negligible for a single-operator local database.
- Semantic search is not provided; lexical matching only.
