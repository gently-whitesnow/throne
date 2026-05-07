#!/usr/bin/env python3
"""Starter cross-file duplication detector for C# repositories.

Tokenizes each .cs file lexically (identifiers -> I, numbers -> N, strings -> S),
slides a window of K logical lines, hashes each window, and reports windows that
appear in two or more places. Cross-file duplication is the default signal: when
loop agents reproduce a slice/handler instead of extracting shared logic, the
same normalized window shows up in multiple files.

This is a heuristic, not a semantic-aware tool. Keep window size large enough
(>= 6) to avoid idiomatic noise (using statements, simple guards, small DTOs).

Config lives in maintainability-budget.json under the optional "duplicates" key:

    {
      "scanRoots": ["src", "tests"],
      "generatedFilePatterns": ["**/Generated/**", ...],
      "duplicates": {
        "windowLines": 6,
        "minOccurrences": 2,
        "crossFileOnly": true,
        "gateMode": "advisory",
        "ignorePathPatterns": ["**/Migrations/**"]
      }
    }
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import os
import pathlib
import sys
from dataclasses import dataclass
from typing import Iterable


DEFAULT_CONFIG_NAMES = (
    "maintainability-budget.json",
    "config/maintainability-budget.json",
)

DEFAULT_EXCLUDE = (
    "**/bin/**",
    "**/obj/**",
    "**/.git/**",
    "**/Generated/**",
    "**/generated/**",
    "**/*.g.cs",
    "**/*.generated.cs",
    "**/*.designer.cs",
    "**/Migrations/**",
)

CSHARP_KEYWORDS = {
    "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case",
    "catch", "char", "checked", "class", "const", "continue", "decimal", "default",
    "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
    "false", "finally", "fixed", "float", "for", "foreach", "global", "goto",
    "if", "implicit", "in", "init", "int", "interface", "internal", "is", "lock",
    "long", "namespace", "new", "null", "object", "operator", "out", "override",
    "params", "partial", "private", "protected", "public", "readonly", "ref",
    "record", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc",
    "static", "string", "struct", "switch", "this", "throw", "true", "try",
    "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
    "virtual", "void", "volatile", "when", "where", "while", "with", "yield",
}

NOISE_LINES = {"{", "}", ";", "})", "});", "};", ");", "({", "()", "()", "}{"}


@dataclass(frozen=True)
class Window:
    file_hash_id: int
    path: str
    start_line: int
    end_line: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Detect cross-file duplication in C# files using normalized window hashing."
    )
    parser.add_argument("roots", nargs="*", help="Scan roots. Defaults to scanRoots in config or src/tests.")
    parser.add_argument(
        "--config",
        default=os.environ.get("MAINTAINABILITY_BUDGET_CONFIG"),
        help="Path to maintainability-budget.json.",
    )
    parser.add_argument("--window-lines", type=int, default=None, help="Window size in logical lines.")
    parser.add_argument("--min-occurrences", type=int, default=None, help="Min duplicate occurrences to report.")
    parser.add_argument(
        "--allow-same-file",
        action="store_true",
        help="Report duplicates within a single file too. Default reports cross-file only.",
    )
    parser.add_argument(
        "--mode",
        choices=("advisory", "blocking"),
        default=os.environ.get("DUPLICATE_GATE_MODE"),
        help="Override gate mode from config.",
    )
    parser.add_argument(
        "--baseline-snapshot",
        default=os.environ.get("DUPLICATE_BASELINE_SNAPSHOT"),
        help="Path to baseline snapshot. Any new duplicate group fails the run (ratchet).",
    )
    parser.add_argument(
        "--write-baseline-snapshot",
        default=None,
        help="Write current duplicate groups as new baseline, then exit 0.",
    )
    parser.add_argument("--max-groups", type=int, default=int(os.environ.get("DUPLICATE_MAX_GROUPS", "30")))
    return parser.parse_args()


def load_config(path: str | None) -> dict:
    if path:
        config_path = pathlib.Path(path)
        if not config_path.exists():
            raise SystemExit(f"Config не найден: {path}")
        return json.loads(config_path.read_text(encoding="utf-8"))

    for candidate in DEFAULT_CONFIG_NAMES:
        config_path = pathlib.Path(candidate)
        if config_path.exists():
            return json.loads(config_path.read_text(encoding="utf-8"))

    return {}


def matches_any(path: str, patterns: Iterable[str]) -> bool:
    for pattern in patterns:
        normalized = pattern.replace("\\", "/")
        if fnmatch.fnmatch(path, normalized):
            return True
        if normalized.startswith("**/") and fnmatch.fnmatch(path, normalized[3:]):
            return True
    return False


def find_csharp_files(roots: list[pathlib.Path], exclude: list[str]) -> list[pathlib.Path]:
    files: list[pathlib.Path] = []
    seen: set[pathlib.Path] = set()
    for root in roots:
        candidates = root.rglob("*.cs") if root.is_dir() else [root]
        for path in candidates:
            if not path.is_file() or path.suffix.lower() != ".cs":
                continue
            normalized = path.as_posix()
            if matches_any(normalized, exclude):
                continue
            resolved = path.resolve()
            if resolved in seen:
                continue
            seen.add(resolved)
            files.append(path)
    return sorted(files)


def is_escaped(text: str, index: int) -> bool:
    backslashes = 0
    i = index - 1
    while i >= 0 and text[i] == "\\":
        backslashes += 1
        i -= 1
    return backslashes % 2 == 1


def normalize_line(line: str, state: dict) -> str:
    out: list[str] = []
    i = 0
    in_str = state.get("in_str", False)
    verbatim = state.get("verbatim", False)
    in_block = state.get("in_block", False)

    while i < len(line):
        ch = line[i]
        nxt = line[i + 1] if i + 1 < len(line) else ""

        if in_block:
            if ch == "*" and nxt == "/":
                in_block = False
                i += 2
                continue
            i += 1
            continue

        if in_str:
            if verbatim and ch == '"' and nxt == '"':
                i += 2
                continue
            if ch == '"' and (verbatim or not is_escaped(line, i)):
                in_str = False
                verbatim = False
                out.append("S")
                i += 1
                continue
            i += 1
            continue

        if ch == "/" and nxt == "/":
            break
        if ch == "/" and nxt == "*":
            in_block = True
            i += 2
            continue
        if ch == "@" and nxt == '"':
            in_str = True
            verbatim = True
            i += 2
            continue
        if ch == "$" and nxt == '"':
            in_str = True
            verbatim = False
            i += 2
            continue
        if ch == '"':
            in_str = True
            verbatim = False
            i += 1
            continue
        if ch.isalpha() or ch == "_":
            j = i
            while j < len(line) and (line[j].isalnum() or line[j] == "_"):
                j += 1
            token = line[i:j]
            out.append(token if token in CSHARP_KEYWORDS else "I")
            i = j
            continue
        if ch.isdigit():
            j = i
            while j < len(line) and (line[j].isalnum() or line[j] in "._"):
                j += 1
            out.append("N")
            i = j
            continue
        if ch.isspace():
            i += 1
            continue
        out.append(ch)
        i += 1

    state["in_str"] = in_str
    state["verbatim"] = verbatim
    state["in_block"] = in_block
    return "".join(out)


def normalize_file(path: pathlib.Path) -> list[tuple[int, str]]:
    text = path.read_text(encoding="utf-8-sig")
    state: dict = {}
    pairs: list[tuple[int, str]] = []
    for index, line in enumerate(text.splitlines(), 1):
        normalized = normalize_line(line, state)
        if not normalized:
            continue
        if normalized in NOISE_LINES:
            continue
        pairs.append((index, normalized))
    return pairs


def collect_windows(
    files: list[pathlib.Path],
    window_lines: int,
) -> dict[str, list[Window]]:
    groups: dict[str, list[Window]] = {}
    for file_id, path in enumerate(files):
        pairs = normalize_file(path)
        if len(pairs) < window_lines:
            continue
        normalized_path = path.as_posix()
        for i in range(len(pairs) - window_lines + 1):
            window_pairs = pairs[i : i + window_lines]
            joined = "\n".join(pair[1] for pair in window_pairs)
            digest = hashlib.sha1(joined.encode("utf-8")).hexdigest()
            groups.setdefault(digest, []).append(
                Window(
                    file_hash_id=file_id,
                    path=normalized_path,
                    start_line=window_pairs[0][0],
                    end_line=window_pairs[-1][0],
                )
            )
    return groups


def filter_groups(
    groups: dict[str, list[Window]],
    min_occurrences: int,
    cross_file_only: bool,
) -> dict[str, list[Window]]:
    result: dict[str, list[Window]] = {}
    for digest, windows in groups.items():
        if len(windows) < min_occurrences:
            continue
        if cross_file_only and len({w.file_hash_id for w in windows}) < 2:
            continue
        result[digest] = windows
    return result


def deduplicate_overlapping(windows: list[Window]) -> list[Window]:
    by_file: dict[str, list[Window]] = {}
    for window in windows:
        by_file.setdefault(window.path, []).append(window)
    representative: list[Window] = []
    for path, items in by_file.items():
        items.sort(key=lambda w: w.start_line)
        last_end = -1
        for item in items:
            if item.start_line > last_end:
                representative.append(item)
                last_end = item.end_line
    return sorted(representative, key=lambda w: (w.path, w.start_line))


def print_report(
    files_count: int,
    groups: dict[str, list[Window]],
    mode: str,
    max_groups: int,
) -> None:
    print(f"==> Duplicate scan ({mode})")
    print(f"Scanned C# files: {files_count}")
    print(f"Duplicate groups: {len(groups)}")

    if not groups:
        print("No duplicate windows detected.")
        return

    sorted_digests = sorted(groups.keys(), key=lambda d: -len(groups[d]))
    print()
    for digest in sorted_digests[:max_groups]:
        windows = deduplicate_overlapping(groups[digest])
        print(f"[{digest[:10]}] {len(windows)} occurrence(s):")
        for window in windows:
            print(f"  {window.path}:{window.start_line}-{window.end_line}")
    remaining = len(sorted_digests) - max_groups
    if remaining > 0:
        print(f"... {remaining} more group(s) not shown.")
    print()
    if mode == "advisory":
        print("Advisory mode: groups reported, exit code remains 0.")
    else:
        print("Blocking mode: extract shared code or document repo-owned exception before merging.")


def write_baseline(path: str, groups: dict[str, list[Window]]) -> None:
    payload = {
        "version": 1,
        "groups": sorted(groups.keys()),
    }
    pathlib.Path(path).write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def load_baseline(path: str) -> set[str]:
    snapshot = pathlib.Path(path)
    if not snapshot.exists():
        print(f"Baseline snapshot не найден: {path}. Все группы считаются новыми.", file=sys.stderr)
        return set()
    payload = json.loads(snapshot.read_text(encoding="utf-8"))
    return set(payload.get("groups") or [])


def main() -> int:
    args = parse_args()
    config = load_config(args.config)
    duplicates_cfg = config.get("duplicates") or {}

    window_lines = args.window_lines or int(duplicates_cfg.get("windowLines", 6))
    min_occurrences = args.min_occurrences or int(duplicates_cfg.get("minOccurrences", 2))
    cross_file_only = (
        not args.allow_same_file
        and bool(duplicates_cfg.get("crossFileOnly", True))
    )
    mode = args.mode or duplicates_cfg.get("gateMode") or "advisory"
    if mode not in {"advisory", "blocking"}:
        raise SystemExit(f"Unsupported gate mode: {mode}")

    if window_lines < 3:
        raise SystemExit("windowLines must be >= 3 to be meaningful.")

    exclude = list(DEFAULT_EXCLUDE)
    exclude.extend(config.get("generatedFilePatterns") or [])
    exclude.extend(duplicates_cfg.get("ignorePathPatterns") or [])

    if args.roots:
        roots_input = args.roots
    else:
        roots_input = config.get("scanRoots") or ["src", "tests"]

    roots = [pathlib.Path(r) for r in roots_input if pathlib.Path(r).exists()]
    if not roots:
        roots = [pathlib.Path(".")]

    files = find_csharp_files(roots, exclude)
    raw_groups = collect_windows(files, window_lines)
    groups = filter_groups(raw_groups, min_occurrences, cross_file_only)

    print_report(len(files), groups, mode, args.max_groups)

    if args.write_baseline_snapshot:
        write_baseline(args.write_baseline_snapshot, groups)
        print(f"Duplicate baseline snapshot written: {args.write_baseline_snapshot} ({len(groups)} group(s))")
        return 0

    if args.baseline_snapshot:
        baseline = load_baseline(args.baseline_snapshot)
        new_groups = {d: w for d, w in groups.items() if d not in baseline}
        print()
        if new_groups:
            print(f"Ratchet FAIL: {len(new_groups)} new duplicate group(s) vs {args.baseline_snapshot}:")
            for digest in list(new_groups)[: args.max_groups]:
                windows = deduplicate_overlapping(new_groups[digest])
                print(f"[{digest[:10]}] {len(windows)} occurrence(s):")
                for window in windows:
                    print(f"  {window.path}:{window.start_line}-{window.end_line}")
            print()
            print("Extract shared code or, if intentional, regenerate baseline:")
            print(f"  scripts/quality/duplicate-check.sh --write-baseline-snapshot {args.baseline_snapshot}")
            return 1
        print(f"Ratchet OK: no new duplicate groups vs {args.baseline_snapshot}.")
        return 0

    if groups and mode == "blocking":
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
