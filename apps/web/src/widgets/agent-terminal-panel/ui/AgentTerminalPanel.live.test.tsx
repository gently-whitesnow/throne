import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import type {
  RunIntentTerminalResponse,
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
const openNativeIntentTerminal =
  vi.fn<
    (intentId: string) => Promise<{ provider: string; session_name: string }>
  >();
const listIntentRepositories = vi.fn<() => Promise<unknown[]>>();

vi.mock("../api/agent-terminal-api", () => ({
  getIntentTerminalSession: (intentId: string) =>
    getIntentTerminalSession(intentId),
  runIntentTerminal: (intentId: string, payload: TerminalRunPayload) =>
    runIntentTerminal(intentId, payload),
  previewIntentTerminal: () =>
    Promise.resolve({ available_skills_for_mode: [] }),
  openNativeIntentTerminal: (intentId: string) =>
    openNativeIntentTerminal(intentId),
  killIntentTerminal: vi.fn(),
  attachIntentTerminalSkills: vi.fn()
}));

type RealtimeHandlers = Record<string, ((payload: unknown) => void)[]>;
const realtimeHandlers: RealtimeHandlers = {};

vi.mock("@/shared/realtime", () => ({
  useRealtimeEvent: (name: string, handler: (payload: unknown) => void) => {
    const list = realtimeHandlers[name] ?? [];
    list.push(handler);
    realtimeHandlers[name] = list;
  }
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

vi.mock("@/entities/repository-binding/api/repository-bindings-api", () => ({
  listIntentRepositories: () => listIntentRepositories(),
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

vi.mock("@/entities/capability/api/capabilities-api", () => ({
  fetchCapabilities: () =>
    Promise.resolve([
      {
        name: "open_in_terminal",
        title: "Открыть в нативном терминале",
        description: "x",
        selected_provider: null,
        providers: [{ name: "wezterm", title: "WezTerm", detected: true }]
      }
    ]),
  setCapabilitySelectedProvider: vi.fn()
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
        model_source: "static",
        login_status: "ready",
        login_detail: null,
        selectable: true
      },
      {
        vendor: "codex",
        label: "Codex",
        supports_effort: true,
        models: ["gpt-5.5", "gpt-5.4", "gpt-5.3-codex"],
        default_model: "gpt-5.5",
        efforts: ["low", "medium", "high", "xhigh"],
        default_effort: "medium",
        model_source: "static",
        login_status: "ready",
        login_detail: null,
        selectable: true
      }
    ]
  };
}

function sessionResponse(
  state: RunIntentTerminalResponse["session_state"],
  launch?: RunIntentTerminalResponse["launch"]
): RunIntentTerminalResponse {
  return {
    intent_id: "intent-1",
    session_name: "throne-intent-1",
    session_state: state,
    bindings: [],
    ...(launch ? { launch } : {})
  };
}

function render() {
  return renderWithQuery(
    <AgentTerminalPanel intentId="intent-1" intentStatus="work" />,
    { withBridge: false }
  );
}

describe("AgentTerminalPanel live viewers", () => {
  beforeEach(() => {
    getIntentTerminalSession.mockReset();
    runIntentTerminal.mockReset();
    openNativeIntentTerminal.mockReset();
    openNativeIntentTerminal.mockResolvedValue({
      provider: "wezterm",
      session_name: "throne-intent-1"
    });
    listIntentRepositories.mockReset();
    listIntentRepositories.mockResolvedValue([]);
    for (const k of Object.keys(realtimeHandlers)) realtimeHandlers[k] = [];
  });

  afterEach(() => {
    cleanup();
  });

  it("авто-открывает встроенный для live-сессии без отдельной кнопки", async () => {
    getIntentTerminalSession.mockResolvedValue(sessionResponse("running"));
    render();

    expect(
      (await screen.findByTestId("terminal-view")).getAttribute("data-attempt")
    ).toBe("1");
    expect(screen.queryByTestId("agent-terminal-open-embedded")).toBeNull();
    expect(screen.getByTestId("agent-terminal-live-badge")).toBeTruthy();
    expect(screen.getByTestId("agent-terminal-kill")).toBeTruthy();
    expect(screen.getByTestId("agent-terminal-open-native")).toBeTruthy();
    expect(screen.queryByTestId("agent-terminal-run")).toBeNull();
    expect(runIntentTerminal).not.toHaveBeenCalled();
  });

  it("уходит во внешний (гасит встроенный) и возвращается обратно", async () => {
    getIntentTerminalSession.mockResolvedValue(sessionResponse("running"));
    render();

    await screen.findByTestId("terminal-view");
    fireEvent.click(screen.getByTestId("agent-terminal-open-native"));

    await waitFor(() => {
      expect(openNativeIntentTerminal).toHaveBeenCalledWith("intent-1");
    });
    expect(screen.queryByTestId("terminal-view")).toBeNull();
    expect(screen.getByTestId("agent-terminal-native-active")).toBeTruthy();

    fireEvent.click(screen.getByTestId("agent-terminal-return-embedded"));

    expect(await screen.findByTestId("terminal-view")).toBeTruthy();
    expect(screen.queryByTestId("agent-terminal-native-active")).toBeNull();
  });

  it("показывает submit-unconfirmed подсказку для live-сессии", async () => {
    getIntentTerminalSession.mockResolvedValue(sessionResponse("running"));
    render();
    await screen.findByTestId("agent-terminal-live-badge");

    realtimeHandlers["terminal.prompt_submit_unconfirmed"].forEach((fn) => {
      fn({ intent_id: "intent-1" });
    });

    expect(
      await screen.findByText(/стартовый промпт не отправился/i)
    ).toBeTruthy();
  });

  it("показывает фактическую ось live-сессии вместо дефолта каталога", async () => {
    getIntentTerminalSession.mockResolvedValue(
      sessionResponse("running", {
        mode: "interview",
        vendor: "codex",
        model: "gpt-5.4",
        effort: "low"
      })
    );
    render();
    await screen.findByTestId("agent-terminal-live-badge");

    const vendor = screen.getByTestId<HTMLSelectElement>(
      "agent-terminal-vendor"
    );
    await waitFor(() => {
      expect(vendor.value).toBe("codex");
    });
    expect(
      screen.getByTestId<HTMLSelectElement>("agent-terminal-model").value
    ).toBe("gpt-5.4");
    expect(
      screen.getByTestId<HTMLSelectElement>("agent-terminal-effort").value
    ).toBe("low");
    expect(
      screen.getByTestId<HTMLSelectElement>("agent-terminal-mode").value
    ).toBe("interview");
    expect(vendor.disabled).toBe(true);
  });
});
