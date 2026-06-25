import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

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
const previewIntentTerminal =
  vi.fn<
    (
      intentId: string,
      mode: TerminalRunMode,
      selectedPartIds: string[] | null
    ) => Promise<IntentTerminalPreviewResponse>
  >();
const listIntentRepositories = vi.fn<() => Promise<unknown[]>>();

vi.mock("../api/agent-terminal-api", () => ({
  getIntentTerminalSession: (intentId: string) =>
    getIntentTerminalSession(intentId),
  runIntentTerminal: (intentId: string, payload: TerminalRunPayload) =>
    runIntentTerminal(intentId, payload),
  previewIntentTerminal: (
    intentId: string,
    mode: TerminalRunMode,
    selectedPartIds: string[] | null
  ) => previewIntentTerminal(intentId, mode, selectedPartIds),
  openNativeIntentTerminal: vi.fn(),
  killIntentTerminal: vi.fn(),
  attachIntentTerminalSkills: vi.fn()
}));

// The panel subscribes to terminal.prompt_submit_unconfirmed via use-terminal-session;
// stub the realtime hook so no EventSource is opened in jsdom.
vi.mock("@/shared/realtime", () => ({
  useRealtimeEvent: vi.fn()
}));

vi.mock("./TerminalView", () => ({
  TerminalView: () => <div data-testid="terminal-view" />
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
  fetchCapabilities: () => Promise.resolve([]),
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
        models: ["opus"],
        default_model: "opus",
        efforts: ["high"],
        default_effort: "high",
        model_source: "static",
        login_status: "ready",
        login_detail: null,
        selectable: true
      }
    ]
  };
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
    mode: "review",
    parts: [],
    available_skills_for_mode: [],
    selected_part_ids: [],
    system_prompt: "RULES",
    user_prompt: "BODY",
    workspace_map: "=== Карта workspace ==="
  };
}

function binding(id: string, pullRequestNumber: number, repo: string) {
  return {
    id,
    provider: "github",
    host: "github.com",
    owner: "octo",
    repo,
    clone_status: "ready",
    pull_request_number: pullRequestNumber,
    created_at: "2026-06-18T12:00:00Z"
  };
}

function render() {
  return renderWithQuery(
    <AgentTerminalPanel intentId="intent-1" intentStatus="work" />,
    { withBridge: false }
  );
}

describe("AgentTerminalPanel review target", () => {
  beforeEach(() => {
    getIntentTerminalSession.mockReset();
    runIntentTerminal.mockReset();
    previewIntentTerminal.mockReset();
    listIntentRepositories.mockReset();
    getIntentTerminalSession.mockResolvedValue(sessionResponse("exited"));
    runIntentTerminal.mockResolvedValue(sessionResponse("running"));
    previewIntentTerminal.mockResolvedValue(previewResponse());
  });

  afterEach(() => {
    cleanup();
  });

  it("скрывает review без PR и не показывает selector при одном PR", async () => {
    listIntentRepositories.mockResolvedValue([]);
    render();

    await waitFor(() => {
      expect(screen.getByTestId("agent-terminal-run")).toBeTruthy();
    });
    expect(modeOptions()).not.toContain("review");

    cleanup();
    listIntentRepositories.mockResolvedValue([binding("binding-1", 42, "api")]);
    render();

    await waitFor(() => {
      expect(modeOptions()).toContain("review");
    });
    fireEvent.change(screen.getByTestId("agent-terminal-mode"), {
      target: { value: "review" }
    });

    expect(screen.queryByTestId("agent-terminal-review-binding")).toBeNull();
  });

  it("при нескольких PR отправляет выбранный binding в /run", async () => {
    listIntentRepositories.mockResolvedValue([
      binding("binding-1", 41, "api"),
      binding("binding-2", 42, "web")
    ]);
    render();

    await waitFor(() => {
      expect(modeOptions()).toContain("review");
    });
    fireEvent.change(screen.getByTestId("agent-terminal-mode"), {
      target: { value: "review" }
    });

    const selector = await screen.findByTestId<HTMLSelectElement>(
      "agent-terminal-review-binding"
    );
    fireEvent.change(selector, { target: { value: "binding-2" } });
    fireEvent.click(screen.getByTestId("agent-terminal-run"));
    fireEvent.click(
      await screen.findByTestId("agent-terminal-preflight-launch")
    );

    await waitFor(() => {
      expect(runIntentTerminal).toHaveBeenCalledTimes(1);
    });
    expect(runIntentTerminal.mock.calls[0][1].reviewBindingId).toBe(
      "binding-2"
    );
  });
});

function modeOptions(): string[] {
  return Array.from(
    screen.getByTestId("agent-terminal-mode").querySelectorAll("option")
  ).map((option) => option.value);
}
