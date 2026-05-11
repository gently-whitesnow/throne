#!/usr/bin/env python3
"""Throne quality verify entrypoint.

Reads .quality/quality.config.json and runs enabled gates in declared order.
The same command human / CI / loop-agent run before declaring "done".

Exit codes:
  0  every selected gate passed
  1  one or more gates failed
  64 config file missing or invalid
"""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import subprocess
import sys
import time
from typing import Callable


CONFIG_PATH = ".quality/quality.config.json"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run Throne quality gates declared in .quality/quality.config.json."
    )
    parser.add_argument(
        "--scope",
        choices=("all", "backend", "frontend", "cli"),
        default="all",
        help="Run only gates with matching scope. Default: all.",
    )
    parser.add_argument(
        "--fast",
        action="store_true",
        help="Skip gates marked slow=true (integration tests, security audits).",
    )
    parser.add_argument(
        "--only",
        action="append",
        default=[],
        help="Run only the listed gate id(s). Repeatable.",
    )
    parser.add_argument(
        "--skip",
        action="append",
        default=[],
        help="Skip the listed gate id(s). Repeatable.",
    )
    parser.add_argument(
        "--list",
        action="store_true",
        help="Print configured gates and exit.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print plan without executing.",
    )
    return parser.parse_args()


def repo_root() -> pathlib.Path:
    here = pathlib.Path(__file__).resolve()
    return here.parent.parent.parent


def load_config(root: pathlib.Path) -> dict:
    path = root / CONFIG_PATH
    if not path.exists():
        print(f"Config not found: {path}", file=sys.stderr)
        raise SystemExit(64)
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        print(f"Config invalid JSON ({path}): {exc}", file=sys.stderr)
        raise SystemExit(64) from exc


def run(cmd: list[str], cwd: pathlib.Path, env: dict[str, str] | None = None) -> int:
    print(f"  $ {' '.join(cmd)} (cwd={cwd.relative_to(repo_root()) if cwd != repo_root() else '.'})")
    process_env = os.environ.copy()
    if env:
        process_env.update(env)
    return subprocess.run(cmd, cwd=cwd, env=process_env).returncode


# ---------- gate runners ----------------------------------------------------

def gate_backend_contracts(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/openapi-verify-generated-clean.sh"], root)


def gate_backend_realtime(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/realtime-verify-coverage.sh"], root)


def gate_backend_format(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/dotnet-format-verify.sh"], root)


def gate_backend_build(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/dotnet-build-warnaserror.sh"], root)


def gate_backend_test_unit(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/dotnet-test.sh", "--unit-only"], root)


def gate_backend_test_integration(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/dotnet-test.sh", "--integration-only"], root)


def gate_backend_maintainability(gate: dict, root: pathlib.Path) -> int:
    cmd = [
        "bash", "scripts/quality/maintainability-budget-check.sh",
        "--config", gate.get("config", ".quality/maintainability-budget.json"),
        "--profile", gate.get("profile", "legacy"),
    ]
    if "ratchet" in gate:
        cmd.extend(["--baseline-snapshot", gate["ratchet"]])
    return run(cmd, root)


def gate_backend_duplicates(gate: dict, root: pathlib.Path) -> int:
    cmd = [
        "bash", "scripts/quality/duplicate-check.sh",
        "--config", gate.get("config", ".quality/maintainability-budget.json"),
    ]
    if "ratchet" in gate:
        cmd.extend(["--baseline-snapshot", gate["ratchet"]])
    return run(cmd, root)


def gate_backend_audit(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/package-audit.sh"], root)


def gate_backend_suppressions(_g: dict, root: pathlib.Path) -> int:
    return run(["python3", "scripts/quality/suppression_audit.py", "check"], root)


def web(root: pathlib.Path) -> pathlib.Path:
    return root / "apps" / "web"


def gate_frontend_deps(_g: dict, root: pathlib.Path) -> int:
    return run(
        ["pnpm", "install", "--frozen-lockfile", "--prefer-offline"],
        web(root),
        env={"CI": "true"},
    )


def gate_frontend_format(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "format:check"], web(root))


def gate_frontend_lint(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "lint"], web(root))


def gate_frontend_typecheck(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "typecheck"], web(root))


def gate_frontend_architecture(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "architecture"], web(root))


def gate_frontend_test(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "test"], web(root))


def gate_frontend_build(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "build"], web(root))


def gate_frontend_audit(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "audit", "--audit-level", "high"], web(root))


def cli(root: pathlib.Path) -> pathlib.Path:
    return root / "apps" / "cli"


def gate_cli_deps(_g: dict, root: pathlib.Path) -> int:
    return run(
        ["pnpm", "install", "--frozen-lockfile", "--prefer-offline"],
        cli(root),
        env={"CI": "true"},
    )


def gate_cli_format(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "format:check"], cli(root))


def gate_cli_lint(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "lint"], cli(root))


def gate_cli_typecheck(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "typecheck"], cli(root))


def gate_cli_test(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "test"], cli(root))


def gate_cli_build(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "build"], cli(root))


def gate_cli_audit(_g: dict, root: pathlib.Path) -> int:
    return run(["pnpm", "audit", "--audit-level", "high"], cli(root))


GATE_RUNNERS: dict[str, Callable[[dict, pathlib.Path], int]] = {
    "backend-contracts": gate_backend_contracts,
    "backend-realtime": gate_backend_realtime,
    "backend-format": gate_backend_format,
    "backend-build": gate_backend_build,
    "backend-test-unit": gate_backend_test_unit,
    "backend-test-integration": gate_backend_test_integration,
    "backend-maintainability": gate_backend_maintainability,
    "backend-duplicates": gate_backend_duplicates,
    "backend-audit": gate_backend_audit,
    "backend-suppressions": gate_backend_suppressions,
    "frontend-deps": gate_frontend_deps,
    "frontend-format": gate_frontend_format,
    "frontend-lint": gate_frontend_lint,
    "frontend-typecheck": gate_frontend_typecheck,
    "frontend-architecture": gate_frontend_architecture,
    "frontend-test": gate_frontend_test,
    "frontend-build": gate_frontend_build,
    "frontend-audit": gate_frontend_audit,
    "cli-deps": gate_cli_deps,
    "cli-format": gate_cli_format,
    "cli-lint": gate_cli_lint,
    "cli-typecheck": gate_cli_typecheck,
    "cli-test": gate_cli_test,
    "cli-build": gate_cli_build,
    "cli-audit": gate_cli_audit,
}


# ---------- orchestration ---------------------------------------------------

def select_gates(config: dict, args: argparse.Namespace) -> list[dict]:
    gates = config.get("gates") or []
    only = set(args.only)
    skip = set(args.skip)

    selected: list[dict] = []
    for gate in gates:
        gate_id = gate.get("id")
        if not gate_id:
            continue
        if not gate.get("enabled", True):
            continue
        if args.scope != "all" and gate.get("scope") != args.scope:
            continue
        if only and gate_id not in only:
            continue
        if gate_id in skip:
            continue
        if args.fast and gate.get("slow", False):
            continue
        selected.append(gate)
    return selected


def list_gates(config: dict) -> None:
    gates = config.get("gates") or []
    print(f"{'id':<32} {'scope':<10} {'enabled':<8} slow")
    print("-" * 60)
    for gate in gates:
        gid = gate.get("id", "?")
        scope = gate.get("scope", "?")
        enabled = "yes" if gate.get("enabled", True) else "no"
        slow = "yes" if gate.get("slow", False) else ""
        print(f"{gid:<32} {scope:<10} {enabled:<8} {slow}")


def main() -> int:
    args = parse_args()
    root = repo_root()
    config = load_config(root)

    if args.list:
        list_gates(config)
        return 0

    selected = select_gates(config, args)
    if not selected:
        print("No gates selected. Use --list to see configured gates.", file=sys.stderr)
        return 0

    results: list[tuple[str, str, float]] = []
    overall = 0
    stop_on_fail = bool(config.get("stopOnFirstFail", False))

    for index, gate in enumerate(selected, 1):
        gate_id = gate["id"]
        runner = GATE_RUNNERS.get(gate_id)
        if runner is None:
            print(f"\n[{index}/{len(selected)}] {gate_id}: UNKNOWN gate id, пропуск", file=sys.stderr)
            results.append((gate_id, "UNKNOWN", 0.0))
            overall = 1
            continue

        print(f"\n==> [{index}/{len(selected)}] {gate_id}")
        if args.dry_run:
            results.append((gate_id, "DRY-RUN", 0.0))
            continue

        started = time.monotonic()
        try:
            rc = runner(gate, root)
        except FileNotFoundError as exc:
            print(f"  COMMAND NOT FOUND: {exc}", file=sys.stderr)
            rc = 127
        elapsed = time.monotonic() - started
        status = "OK" if rc == 0 else f"FAIL({rc})"
        results.append((gate_id, status, elapsed))
        if rc != 0:
            overall = 1
            if stop_on_fail:
                break

    print_summary(results, overall)
    return overall


def print_summary(results: list[tuple[str, str, float]], overall: int) -> None:
    print()
    print("=" * 64)
    print("Quality verify summary")
    print("=" * 64)
    for gate_id, status, elapsed in results:
        suffix = f" ({elapsed:.1f}s)" if elapsed > 0 else ""
        print(f"  {gate_id:<32} {status}{suffix}")
    print("=" * 64)
    print("RESULT:", "PASS" if overall == 0 else "FAIL")


if __name__ == "__main__":
    raise SystemExit(main())
