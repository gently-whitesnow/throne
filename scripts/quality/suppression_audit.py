#!/usr/bin/env python3
"""Audit per-file analyzer suppressions in apps/api/.editorconfig and apps/api/**/*.cs.

Tracks every `[path] dotnet_diagnostic.CAxxxx.severity = none` entry in
`apps/api/.editorconfig` plus every `#pragma warning disable` in `.cs` source.
Each suppression must carry a justification: a #/`//`-prefixed comment within
the preceding 15 lines mentioning either `intent:` or `ADR-`.

Two failure modes:
  1. unjustified — entry without intent/ADR reference within 15 preceding lines.
  2. unbaselined — new entry vs. baseline snapshot (ratchet: count can only fall).

Subcommands:
  check                      — fail if any unjustified entry exists OR any new
                               entry not present in baseline.
  write-baseline             — overwrite `.quality/suppress-baseline.json`.
  list                       — print current entries; exit 0 always.

Baseline format: JSON list of {kind, location, rule}. `kind` is "editorconfig"
or "pragma"; `location` is `path:line-range` (line of the entry, not of the
comment); `rule` is the diagnostic id.
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
from dataclasses import dataclass, asdict
from typing import Iterable


REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent.parent
EDITORCONFIG = REPO_ROOT / "apps" / "api" / ".editorconfig"
BASELINE = REPO_ROOT / ".quality" / "suppress-baseline.json"
API_ROOT = REPO_ROOT / "apps" / "api"

JUSTIFICATION_RE = re.compile(r"(intent:[0-9a-fA-F]{16,}|ADR-\d{3,4})")
LOOKBACK = 15
PRAGMA_RE = re.compile(r"#pragma\s+warning\s+disable\s+([A-Z0-9, ]+)")
SEVERITY_NONE_RE = re.compile(
    r"^dotnet_diagnostic\.(CA\d{3,5})\.severity\s*=\s*none\s*$"
)
SECTION_RE = re.compile(r"^\[(.+)\]\s*$")
EXCLUDED_GLOBS = ("**/Generated/**", "**/*.g.cs", "**/Migrations/**", "**/bin/**", "**/obj/**")


@dataclass(frozen=True, order=True)
class Entry:
    kind: str
    location: str
    rule: str

    def to_dict(self) -> dict:
        return asdict(self)


def is_excluded(path: pathlib.Path) -> bool:
    rel = path.relative_to(REPO_ROOT).as_posix()
    return (
        "/Generated/" in rel
        or rel.endswith(".g.cs")
        or "/Migrations/" in rel
        or "/bin/" in rel
        or "/obj/" in rel
    )


def parse_editorconfig() -> list[tuple[Entry, bool, str]]:
    """Return list of (entry, has_justification, justification_snippet)."""
    lines = EDITORCONFIG.read_text(encoding="utf-8").splitlines()
    results: list[tuple[Entry, bool, str]] = []

    current_section: str | None = None
    current_section_line: int = 0

    for idx, raw in enumerate(lines, start=1):
        line = raw.strip()
        section_match = SECTION_RE.match(line)
        if section_match:
            current_section = section_match.group(1)
            current_section_line = idx
            continue
        sev = SEVERITY_NONE_RE.match(line)
        if not sev or not current_section:
            continue
        # only per-file sections matter — skip globs like "**/tests/**/*.cs" that
        # are systemic (e.g. CA1707 for xUnit snake_case naming).
        if "*" in current_section:
            continue
        rule = sev.group(1)
        # only track maintainability-class analyzers
        if rule not in {"CA1501", "CA1502", "CA1505", "CA1506"}:
            continue
        location = f"apps/api/.editorconfig:{idx}"
        entry = Entry(kind="editorconfig", location=current_section, rule=rule)
        snippet, ok = scan_justification(lines, current_section_line)
        results.append((entry, ok, snippet))

    return results


def scan_justification(lines: list[str], section_line: int) -> tuple[str, bool]:
    """Look at LOOKBACK lines above the section header for an intent/ADR ref."""
    start = max(0, section_line - 1 - LOOKBACK)
    end = section_line - 1  # 0-based index of the [section] line
    window = lines[start:end]
    # collect contiguous comment block immediately above section, ignoring blanks
    block: list[str] = []
    for raw in reversed(window):
        stripped = raw.strip()
        if not stripped:
            if block:
                # a blank breaks the block once we already have comments
                break
            continue
        if stripped.startswith("#"):
            block.append(stripped.lstrip("# ").rstrip())
            continue
        # any non-comment, non-blank line breaks the block
        break
    block.reverse()
    snippet = " | ".join(block)
    ok = bool(JUSTIFICATION_RE.search(snippet))
    return snippet, ok


def parse_pragmas() -> list[tuple[Entry, bool, str]]:
    """Find every `#pragma warning disable CAxxxx` in non-generated .cs files."""
    results: list[tuple[Entry, bool, str]] = []
    for cs_file in API_ROOT.rglob("*.cs"):
        if is_excluded(cs_file):
            continue
        try:
            lines = cs_file.read_text(encoding="utf-8").splitlines()
        except UnicodeDecodeError:
            continue
        for idx, raw in enumerate(lines, start=1):
            match = PRAGMA_RE.search(raw)
            if not match:
                continue
            rules = [r.strip() for r in match.group(1).split(",") if r.strip()]
            rel = cs_file.relative_to(REPO_ROOT).as_posix()
            for rule in rules:
                snippet, ok = scan_pragma_justification(lines, idx)
                entry = Entry(kind="pragma", location=f"{rel}:{idx}", rule=rule)
                results.append((entry, ok, snippet))
    return results


def scan_pragma_justification(lines: list[str], pragma_line: int) -> tuple[str, bool]:
    """Look at LOOKBACK lines above the pragma for an intent/ADR comment."""
    start = max(0, pragma_line - 1 - LOOKBACK)
    end = pragma_line - 1
    window = lines[start:end]
    block: list[str] = []
    for raw in reversed(window):
        stripped = raw.strip()
        if not stripped:
            if block:
                break
            continue
        if stripped.startswith("//"):
            block.append(stripped.lstrip("/ ").rstrip())
            continue
        break
    block.reverse()
    snippet = " | ".join(block)
    ok = bool(JUSTIFICATION_RE.search(snippet))
    return snippet, ok


def collect_all() -> list[tuple[Entry, bool, str]]:
    return parse_editorconfig() + parse_pragmas()


def load_baseline() -> set[Entry]:
    if not BASELINE.exists():
        return set()
    raw = json.loads(BASELINE.read_text(encoding="utf-8"))
    return {Entry(**item) for item in raw.get("entries", [])}


def write_baseline(entries: Iterable[Entry]) -> None:
    payload = {
        "$schema": "https://example.invalid/throne-suppress-baseline.schema.json",
        "description": "Per-file analyzer suppression baseline. Ratcheted: count can only decrease.",
        "entries": [e.to_dict() for e in sorted(set(entries))],
    }
    BASELINE.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def cmd_list() -> int:
    all_entries = collect_all()
    print(f"Found {len(all_entries)} per-file analyzer suppressions.\n")
    for entry, ok, snippet in sorted(all_entries, key=lambda e: (e[0].kind, e[0].location, e[0].rule)):
        flag = "OK " if ok else "??? "
        print(f"  {flag} [{entry.kind}] {entry.location} :: {entry.rule}")
        if snippet:
            print(f"       └─ {snippet[:140]}")
    return 0


def cmd_write_baseline() -> int:
    entries = [e for e, _, _ in collect_all()]
    new_set = set(entries)
    existing = load_baseline()
    if existing and len(new_set) > len(existing):
        print(
            f"Ratchet REFUSED: suppress baseline вырос с {len(existing)} до {len(new_set)} записей.\n"
            f"Baseline only-down. Удали suppression в коде вместо re-baseline.\n"
            f"Если нужна санкция на рост (deliberate intent), позови оператора через\n"
            f"  set_intent_status(intent_id=<current>, status='needs_help', reason='нужен рост suppress baseline: …').",
            file=sys.stderr,
        )
        return 1
    write_baseline(entries)
    print(f"Wrote {len(new_set)} entries to {BASELINE.relative_to(REPO_ROOT)}.")
    return 0


def cmd_check() -> int:
    all_entries = collect_all()
    baseline = load_baseline()

    current_set = {e for e, _, _ in all_entries}
    justified_map = {e: (ok, snippet) for e, ok, snippet in all_entries}
    new_entries = current_set - baseline
    removed = baseline - current_set

    # baselined entries are grandfathered (existing debt); only new entries must
    # carry a per-section intent:/ADR- reference within preceding LOOKBACK lines.
    new_unjustified = [
        (e, justified_map[e][1]) for e in new_entries if not justified_map[e][0]
    ]

    print(f"Total suppressions: {len(current_set)} (baseline: {len(baseline)})")
    if removed:
        print(f"  ✓ removed {len(removed)} entries vs baseline (ratchet down — re-run write-baseline to lock in)")
        for entry in sorted(removed):
            print(f"      − [{entry.kind}] {entry.location} :: {entry.rule}")

    fail = False

    if new_unjustified:
        print(
            f"\n✗ {len(new_unjustified)} NEW suppression(s) without intent:/ADR- "
            f"reference in preceding {LOOKBACK} lines."
        )
        print("   New suppressions MUST cite an intent id or ADR number in a comment")
        print("   block immediately above. Fix the root cause OR add a justification.")
        for entry, snippet in new_unjustified:
            print(f"    [{entry.kind}] {entry.location} :: {entry.rule}")
            print(f"        preceding comment: {snippet or '<none>'}")
        fail = True

    other_new = [e for e in new_entries if justified_map[e][0]]
    if other_new:
        print(
            f"\n✗ {len(other_new)} NEW suppression(s) (justified, but not in baseline)."
        )
        print("   Ratchet policy: any new suppression — even justified — requires")
        print("   re-baselining with explicit rationale in the commit message.")
        for entry in sorted(other_new):
            print(f"    [{entry.kind}] {entry.location} :: {entry.rule}")
        fail = True

    if not fail:
        print("\n✓ no new suppressions vs baseline.")
    return 1 if fail else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "command",
        choices=("check", "write-baseline", "list"),
        nargs="?",
        default="check",
    )
    args = parser.parse_args()
    if args.command == "check":
        return cmd_check()
    if args.command == "write-baseline":
        return cmd_write_baseline()
    if args.command == "list":
        return cmd_list()
    return 64


if __name__ == "__main__":
    sys.exit(main())
