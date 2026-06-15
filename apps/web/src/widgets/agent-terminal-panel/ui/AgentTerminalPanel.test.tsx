import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import type { Capability } from "@/entities/capability";

import type {
  IntentTerminalPreviewResponse,
  RunIntentTerminalResponse,
  TerminalRunMode,
  TerminalRunPayload
} from "../model/types";

import { AgentTerminalPanel } from "./AgentTerminalPanel";

const getIntentTerminalSession =
  vi.fn<(intentId: string) => Promise<RunIntentTerminalResponse>>();
const runIntentTerminal =
  vi.fn<
    (
      intentId: string,
      payload: TerminalRunPayload
    ) => Promise<RunIntentTerminalResponse>
  >();
const restartIntentTerminal = vi.fn<() => Promise<RunIntentTerminalResponse>>();
const previewIntentTerminal =
  vi.fn<
    (
      intentId: string,
      mode: TerminalRunMode,
      selectedPartIds: string[] | null
    ) => Promise<IntentTerminalPreviewResponse>
  >();

vi.mock("../api/agent-terminal-api", () => ({
  getIntentTerminalSession: (intentId: string) =>
    getIntentTerminalSession(intentId),
  runIntentTerminal: (intentId: string, payload: TerminalRunPayload) =>
    runIntentTerminal(intentId, payload),
  restartIntentTerminal: () => restartIntentTerminal(),
  previewIntentTerminal: (
    intentId: string,
    mode: TerminalRunMode,
    selectedPartIds: string[] | null
  ) => previewIntentTerminal(intentId, mode, selectedPartIds)
}));

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
  unbindIntentRepository: vi.fn()
}));

vi.mock("@/entities/terminal-setting/api/terminal-vendor-catalog-api", () => ({
  fetchTerminalVendorCatalog: () => Promise.resolve(vendorCatalog())
}));

vi.mock("@/entities/terminal-setting/api/terminal-settings-api", () => ({
  fetchTerminalSettings: () => Promise.resolve({ default_vendor: "claude" }),
  setDefaultTerminalVendor: vi.fn()
}));

function vendorCatalog() {
  return {
    default_vendor: "claude",
    vendors: [
      {
        vendor: "claude",
        label: "Claude",
        supports_effort: true,
        models: ["opus", "sonnet", "haiku"],
        default_model: "opus",
        efforts: ["low", "medium", "high", "xhigh"],
        default_effort: "high",
        model_source: "static"
      },
      {
        vendor: "codex",
        label: "Codex",
        supports_effort: true,
        models: ["gpt-5.5", "gpt-5.4", "gpt-5.3-codex"],
        default_model: "gpt-5.5",
        efforts: ["low", "medium", "high", "xhigh"],
        default_effort: "medium",
        model_source: "static"
      }
    ]
  };
}

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

function previewResponse(): IntentTerminalPreviewResponse {
  return {
    intent_id: "intent-1",
    intent_version: 2,
    mode: "free",
    parts: [],
    selected_part_ids: [],
    system_prompt: "RULES",
    user_prompt: "BODY"
  };
}

const render = () =>
  renderWithQuery(
    <AgentTerminalPanel intentId="intent-1" intentStatus="work" />,
    { withBridge: false }
  );

describe("AgentTerminalPanel", () => {
  beforeEach(() => {
    getIntentTerminalSession.mockReset();
    runIntentTerminal.mockReset();
    restartIntentTerminal.mockReset();
    previewIntentTerminal.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("при живой сессии после mount показывает терминал и не зовёт /run", async () => {
    getIntentTerminalSession.mockResolvedValue(sessionResponse("running"));

    render();

    const terminal = await screen.findByTestId("terminal-view");
    expect(terminal.getAttribute("data-attempt")).toBe("1");

    const mode = screen.getByTestId("agent-terminal-mode");
    expect(mode.hasAttribute("disabled")).toBe(true);
    expect(screen.getByTestId("agent-terminal-restart")).toBeTruthy();
    expect(screen.queryByTestId("agent-terminal-run")).toBeNull();
    expect(runIntentTerminal).not.toHaveBeenCalled();
  });

  it("Run открывает модалку, а /run уходит только после подтверждения с собранным payload", async () => {
    getIntentTerminalSession.mockResolvedValue(sessionResponse("exited"));
    previewIntentTerminal.mockResolvedValue(previewResponse());
    runIntentTerminal.mockResolvedValue(sessionResponse("running"));

    render();

    await waitFor(() => {
      expect(screen.getByTestId("agent-terminal-run")).toBeTruthy();
    });

    const vendor = screen.getByRole<HTMLSelectElement>("combobox", {
      name: "Агент терминала"
    });
    fireEvent.change(vendor, { target: { value: "codex" } });

    // До открытия модалки и подтверждения /run не уходит.
    fireEvent.click(screen.getByTestId("agent-terminal-run"));
    expect(runIntentTerminal).not.toHaveBeenCalled();

    const launch = await screen.findByTestId("agent-terminal-preflight-launch");
    await waitFor(() => {
      expect(previewIntentTerminal).toHaveBeenCalledWith(
        "intent-1",
        "free",
        null
      );
    });

    fireEvent.click(launch);

    await waitFor(() => {
      expect(runIntentTerminal).toHaveBeenCalledTimes(1);
    });
    expect(runIntentTerminal).toHaveBeenCalledWith("intent-1", {
      launch: {
        mode: "free",
        vendor: "codex",
        model: "gpt-5.5",
        effort: "medium"
      },
      selectedPartIds: [],
      systemPrompt: "RULES",
      userPrompt: "BODY",
      intentTextUpdate: null
    });
  });
});
