import { cleanup, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import type { Capability } from "@/entities/capability";

import type { RunIntentTerminalResponse } from "../model/types";

import { AgentTerminalPanel } from "./AgentTerminalPanel";

const getIntentTerminalSession =
  vi.fn<(intentId: string) => Promise<RunIntentTerminalResponse>>();
const runIntentTerminal = vi.fn<() => Promise<RunIntentTerminalResponse>>();
const restartIntentTerminal = vi.fn<() => Promise<RunIntentTerminalResponse>>();

// Панель читает api через относительный путь — мок подменяет именно его,
// оставляя публичный barrel и хук reattach (use-terminal-session) настоящими.
vi.mock("../api/agent-terminal-api", () => ({
  getIntentTerminalSession: (intentId: string) =>
    getIntentTerminalSession(intentId),
  runIntentTerminal: () => runIntentTerminal(),
  restartIntentTerminal: () => restartIntentTerminal()
}));

// xterm + WebSocket из TerminalView недоступны в jsdom; sentinel фиксирует факт
// монтирования terminal-блока и nonce-попытку, по которой подключается сокет.
vi.mock("./TerminalView", () => ({
  TerminalView: ({
    intentId,
    attempt
  }: {
    intentId: string;
    attempt: number;
  }) => (
    <div data-testid="terminal-view" data-attempt={attempt}>
      {intentId}
    </div>
  )
}));

vi.mock("@/entities/capability/api/capabilities-api", () => ({
  fetchCapabilities: () => Promise.resolve(terminalCapability()),
  setCapabilityEnabled: vi.fn()
}));

vi.mock("@/entities/repository-binding/api/repository-bindings-api", () => ({
  listIntentRepositories: () => Promise.resolve([]),
  bindIntentRepository: vi.fn(),
  unbindIntentRepository: vi.fn(),
  searchGithubRepositories: vi.fn(),
  listMyGithubRepositories: vi.fn()
}));

function terminalCapability(): Capability[] {
  return [
    {
      name: "terminal",
      title: "Терминал агента",
      description: "tmux-сессия агента на странице интента.",
      prerequisite_hint: "brew install tmux",
      detected: true,
      enabled: true
    }
  ];
}

function sessionResponse(
  state: RunIntentTerminalResponse["session_state"]
): RunIntentTerminalResponse {
  return {
    intent_id: "intent-1",
    session_name: "throne-intent-1",
    session_state: state,
    bindings: []
  };
}

const render = () =>
  renderWithQuery(
    <AgentTerminalPanel intentId="intent-1" intentStatus="work" />,
    { withBridge: false }
  );

describe("AgentTerminalPanel — reattach живой tmux-сессии", () => {
  beforeEach(() => {
    getIntentTerminalSession.mockReset();
    runIntentTerminal.mockReset();
    restartIntentTerminal.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("при живой сессии после mount показывает терминал и подключается без повторного /run", async () => {
    getIntentTerminalSession.mockResolvedValue(sessionResponse("running"));

    render();

    const terminal = await screen.findByTestId("terminal-view");
    expect(terminal.getAttribute("data-attempt")).toBe("1");

    const mode = screen.getByTestId("agent-terminal-mode");
    const copy = screen.getByTestId("agent-terminal-copy");
    expect(mode.hasAttribute("disabled")).toBe(true);
    expect(copy.hasAttribute("disabled")).toBe(true);

    expect(screen.getByTestId("agent-terminal-restart")).toBeTruthy();
    expect(screen.queryByTestId("agent-terminal-run")).toBeNull();

    expect(getIntentTerminalSession).toHaveBeenCalledTimes(1);
    expect(runIntentTerminal).not.toHaveBeenCalled();
  });

  it("без живой сессии терминал-блок не появляется, контролы активны", async () => {
    getIntentTerminalSession.mockResolvedValue(sessionResponse("exited"));

    render();

    await waitFor(() => {
      expect(screen.getByTestId("agent-terminal-run")).toBeTruthy();
    });

    const mode = screen.getByTestId("agent-terminal-mode");
    expect(mode.hasAttribute("disabled")).toBe(false);
    expect(screen.queryByTestId("terminal-view")).toBeNull();
    expect(runIntentTerminal).not.toHaveBeenCalled();
  });
});
