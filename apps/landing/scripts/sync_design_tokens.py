#!/usr/bin/env python3
"""Sync the landing theme CSS from the canonical DESIGN.md.

The repo-root DESIGN.md frontmatter is the single source of design tokens
for Throne (apps/web reads it too). This script reads it and emits
apps/landing/src/app/styles/tokens.generated.css using the public-landing
token names (`--color-canvas`, `--color-surface`, etc.) — DaisyUI is NOT
used here, so we expose semantic aliases directly rather than the `base-*`
shape.

Usage:
  python apps/landing/scripts/sync_design_tokens.py            # write file
  python apps/landing/scripts/sync_design_tokens.py --check    # drift check
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import yaml

LANDING_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = Path(__file__).resolve().parents[3]
DESIGN_MD = REPO_ROOT / "DESIGN.md"
TARGET = LANDING_ROOT / "src" / "app" / "styles" / "tokens.generated.css"

HEADER = (
    "/*\n"
    " * AUTO-GENERATED FROM DESIGN.md — DO NOT EDIT BY HAND.\n"
    " * Regenerate with: python apps/landing/scripts/sync_design_tokens.py\n"
    " */\n"
)

# Semantic tokens consumed by the public landing.
# Order is fixed for stable diffs.
TOKEN_KEYS: tuple[str, ...] = (
    "primary",
    "primary-strong",
    "secondary",
    "accent",
    "accent-strong",
    "neutral",
    "neutral-soft",
    "canvas",
    "surface",
    "border",
    "text",
    "text-muted",
    "text-subtle",
    "info",
    "info-soft",
    "success",
    "success-soft",
    "warning",
    "warning-soft",
    "error",
    "error-soft",
)


def parse_frontmatter(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---"):
        raise SystemExit(f"{path}: frontmatter not found (must start with ---).")
    end = text.find("\n---", 3)
    if end == -1:
        raise SystemExit(f"{path}: frontmatter not closed.")
    body = text[3:end].lstrip("\n").rstrip()
    data = yaml.safe_load(body)
    if not isinstance(data, dict):
        raise SystemExit(f"{path}: frontmatter is not a mapping.")
    return data


def render_block(selector: str, tokens: dict[str, str]) -> str:
    missing = [k for k in TOKEN_KEYS if k not in tokens]
    if missing:
        raise SystemExit(
            f"{selector}: vendored DESIGN.md is missing tokens: "
            f"{', '.join(missing)}"
        )
    lines = [f"{selector} {{"]
    for key in TOKEN_KEYS:
        lines.append(f"  --color-{key}: {tokens[key]};")
    lines.append("}")
    return "\n".join(lines)


def build(fm: dict) -> str:
    colors = fm.get("colors")
    if not isinstance(colors, dict):
        raise SystemExit("DESIGN.md: 'colors' mapping is required.")
    dark_overrides = fm.get("dark") or {}
    if not isinstance(dark_overrides, dict):
        raise SystemExit("DESIGN.md: 'dark' must be a mapping if present.")
    light = {k: str(v) for k, v in colors.items()}
    dark = {**light, **{k: str(v) for k, v in dark_overrides.items()}}
    chunks = [
        HEADER,
        render_block(":root", light),
        "",
        render_block("[data-theme='dark']", dark),
        "",
    ]
    return "\n".join(chunks)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--check",
        action="store_true",
        help="Verify TARGET is in sync with the vendored DESIGN.md.",
    )
    args = ap.parse_args()

    fm = parse_frontmatter(DESIGN_MD)
    content = build(fm)

    if args.check:
        current = TARGET.read_text(encoding="utf-8") if TARGET.exists() else ""
        if current != content:
            sys.stderr.write(
                f"[design] drift: {TARGET.relative_to(REPO_ROOT)} is out of "
                "sync with DESIGN.md.\n"
                "Run: python apps/landing/scripts/sync_design_tokens.py\n"
            )
            return 1
        sys.stdout.write(
            f"[design] ok: {TARGET.relative_to(REPO_ROOT)} matches DESIGN.md\n"
        )
        return 0

    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_text(content, encoding="utf-8")
    sys.stdout.write(
        f"[design] wrote {TARGET.relative_to(REPO_ROOT)} from DESIGN.md\n"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
