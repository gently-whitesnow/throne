#!/usr/bin/env python3
"""Sync apps/web theme CSS from DESIGN.md.

DESIGN.md frontmatter is the canonical source of design tokens for Throne.
This script reads it and writes apps/web/src/app/styles/tokens.generated.css
with [data-theme="throne-light"] and [data-theme="throne-dark"] blocks for
DaisyUI to pick up at runtime.

Usage:
  python scripts/design/sync_design_tokens.py            # write generated file
  python scripts/design/sync_design_tokens.py --check    # exit non-zero on drift
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
DESIGN_MD = REPO_ROOT / "DESIGN.md"
TARGET = REPO_ROOT / "apps/web/src/app/styles/tokens.generated.css"

HEADER = (
    "/*\n"
    " * AUTO-GENERATED FROM DESIGN.md — DO NOT EDIT BY HAND.\n"
    " * Regenerate with: python scripts/design/sync_design_tokens.py\n"
    " */\n"
)

# DaisyUI theme tokens. Order is fixed to keep diffs stable.
DAISY_KEYS: tuple[str, ...] = (
    "primary",
    "primary-content",
    "secondary",
    "secondary-content",
    "accent",
    "accent-content",
    "neutral",
    "neutral-content",
    "base-100",
    "base-200",
    "base-300",
    "base-content",
    "info",
    "info-content",
    "success",
    "success-content",
    "warning",
    "warning-content",
    "error",
    "error-content",
)

# Extra semantic tokens exposed as plain --color-* custom properties.
# Harmless for DaisyUI (it ignores unknown --color-*), useful for any
# component that needs strong/soft/muted variants directly.
EXTRA_KEYS: tuple[str, ...] = (
    "primary-strong",
    "accent-strong",
    "canvas",
    "surface",
    "neutral-soft",
    "border",
    "text",
    "text-muted",
    "text-subtle",
    "info-soft",
    "success-soft",
    "warning-soft",
    "error-soft",
    # Status-badge palette (intent lifecycle, clone state, PR state).
    "status-neutral-surface",
    "status-neutral-ink",
    "status-info-surface",
    "status-info-ink",
    "status-info-strong-ink",
    "status-progress-surface",
    "status-progress-ink",
    "status-review-surface",
    "status-review-ink",
    "status-attention-surface",
    "status-attention-ink",
    "status-success-surface",
    "status-success-ink",
    "status-danger-surface",
    "status-danger-ink",
    "status-archive-surface",
    "status-archive-ink",
    "status-merged-surface",
    "status-merged-ink",
    # Embedded terminal surface.
    "terminal-bg",
    "terminal-fg",
    "terminal-cursor",
    # Family-marker stripe hues (intent board).
    "family-1",
    "family-2",
    "family-3",
    "family-4",
    "family-5",
    "family-6",
)

ALL_KEYS: tuple[str, ...] = DAISY_KEYS + EXTRA_KEYS


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


def render_theme(theme_name: str, tokens: dict[str, str]) -> str:
    missing = [k for k in ALL_KEYS if k not in tokens]
    if missing:
        raise SystemExit(
            f"theme '{theme_name}' is missing tokens: {', '.join(missing)}"
        )
    lines = [f'[data-theme="{theme_name}"] {{']
    for key in ALL_KEYS:
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
        render_theme("throne-light", light),
        "",
        render_theme("throne-dark", dark),
        "",
    ]
    return "\n".join(chunks)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--check",
        action="store_true",
        help="Verify TARGET is in sync with DESIGN.md (no write).",
    )
    args = ap.parse_args()

    fm = parse_frontmatter(DESIGN_MD)
    content = build(fm)

    if args.check:
        current = TARGET.read_text(encoding="utf-8") if TARGET.exists() else ""
        if current != content:
            sys.stderr.write(
                f"[design] drift: {TARGET.relative_to(REPO_ROOT)} is out of sync "
                "with DESIGN.md.\n"
                "Run: python scripts/design/sync_design_tokens.py\n"
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
