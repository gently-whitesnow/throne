#!/usr/bin/env python3
"""Frontend file-size budget (LOC) check.

Аналог backend maintainability budget, но для apps/web (.ts/.tsx). Один лимит —
строк на файл — плюс ratchet через baseline-снимок: уже существующие нарушители
заморожены на своём размере (расти им нельзя), новые файлы обязаны влезать в
лимит. Generated- и test-файлы исключены.

Usage:
  python scripts/quality/frontend_loc_check.py --config .quality/frontend-budget.json \
      --baseline-snapshot .quality/frontend-loc-baseline.json
  python scripts/quality/frontend_loc_check.py --config ... --update-baseline

Exit codes:
  0  всё в бюджете (или baseline обновлён)
  1  есть нарушения
  64 конфиг отсутствует/невалиден
"""
from __future__ import annotations

import argparse
import fnmatch
import json
import pathlib
import sys

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]


def load_json(path: pathlib.Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        sys.stderr.write(f"[frontend-loc] config not found: {path}\n")
        raise SystemExit(64)
    except json.JSONDecodeError as exc:
        sys.stderr.write(f"[frontend-loc] invalid JSON in {path}: {exc}\n")
        raise SystemExit(64)


def is_excluded(rel: str, patterns: list[str]) -> bool:
    return any(fnmatch.fnmatch(rel, pat) for pat in patterns)


def collect_files(config: dict) -> dict[str, int]:
    roots = config.get("scanRoots", [])
    extensions = tuple(config.get("extensions", [".ts", ".tsx"]))
    excludes = config.get("excludeGlobs", [])
    sizes: dict[str, int] = {}
    for root in roots:
        base = REPO_ROOT / root
        if not base.exists():
            continue
        for path in base.rglob("*"):
            if not path.is_file() or path.suffix not in extensions:
                continue
            rel = path.relative_to(REPO_ROOT).as_posix()
            if is_excluded(rel, excludes):
                continue
            loc = len(path.read_text(encoding="utf-8").splitlines())
            sizes[rel] = loc
    return sizes


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--config", required=True)
    ap.add_argument("--baseline-snapshot")
    ap.add_argument(
        "--update-baseline",
        action="store_true",
        help="Перезаписать baseline текущими нарушителями (требует --baseline-snapshot).",
    )
    args = ap.parse_args()

    config = load_json(REPO_ROOT / args.config)
    limit = int(config.get("fileMaxLoc", 300))
    sizes = collect_files(config)

    baseline: dict[str, int] = {}
    baseline_path = (
        REPO_ROOT / args.baseline_snapshot if args.baseline_snapshot else None
    )
    if baseline_path and baseline_path.exists():
        baseline = {k: int(v) for k, v in load_json(baseline_path).items()}

    if args.update_baseline:
        if not baseline_path:
            sys.stderr.write("[frontend-loc] --update-baseline requires --baseline-snapshot\n")
            return 64
        offenders = {p: loc for p, loc in sorted(sizes.items()) if loc > limit}
        baseline_path.write_text(
            json.dumps(offenders, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
        sys.stdout.write(
            f"[frontend-loc] baseline updated: {len(offenders)} grandfathered file(s)\n"
        )
        return 0

    violations: list[str] = []
    for rel, loc in sorted(sizes.items()):
        allowed = baseline.get(rel, limit)
        if loc > allowed:
            reason = (
                f"растёт сверх baseline {allowed}"
                if rel in baseline
                else f"лимит {limit}"
            )
            violations.append(f"  {rel}: {loc} LOC ({reason})")

    stale = [rel for rel, loc in baseline.items() if rel not in sizes or sizes[rel] <= limit]

    if violations:
        sys.stderr.write(
            "[frontend-loc] файлы превышают бюджет — декомпозируйте:\n"
            + "\n".join(violations)
            + "\n"
        )
        return 1

    if stale:
        sys.stdout.write(
            "[frontend-loc] ok. Baseline можно подтянуть (файлы ушли в бюджет): "
            + ", ".join(stale)
            + "\n"
        )
    else:
        sys.stdout.write(
            f"[frontend-loc] ok: {len(sizes)} файлов в бюджете (лимит {limit}).\n"
        )
    return 0


if __name__ == "__main__":
    sys.exit(main())
