import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { IntentStatus } from "@/entities/intent";
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
const killIntentTerminal =
  vi.fn<(intentId: string) => Promise<RunIntentTerminalResponse>>();
const listIntentRepositories = vi.fn<() => Promise<unknown[]>>();

vi.mock("../api/agent-terminal-api", () => ({
  getIntentTerminalSession: (intentId: string) =>
    getIntentTerminalSession(intentId),
  runIntentTerminal: (intentId: string, payload: TerminalRunPayload) =>
    runIntentTerminal(intentId, payload),
  previewIntentTerminal: () =>
    Promise.resolve({
      intent_id: "intent-1",
      intent_version: 2,
      mode: "work",
      parts: [],
      available_skills_for_mode: [],
      selected_part_ids: [],
      system_prompt: "RULES",
      user_prompt: "BODY",
      workspace_map: "=== Карта workspace ==="
    }),
  openNativeIntentTerminal: vi.fn(),
  killIntentTerminal: (intentId: string) => killIntentTerminal(intentId),
  attachIntentTerminalSkills: vi.fn()
}));

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
  fetchTerminalVendorCatalog: () =>
    Promise.resolve({
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
    })
}));

vi.mock("@/entities/terminal-setting/api/terminal-settings-api", () => ({
  fetchTerminalSettings: () => Promise.resolve({ default_vendor: "claude" }),
  setDefaultTerminalVendor: vi.fn()
}));

vi.mock("@/entities/capability/api/capabilities-api", () => ({
  fetchCapabilities: () => Promise.resolve([]),
  setCapabilitySelectedProvider: vi.fn()
}));

const INTERVIEW_LAUNCH = {
  mode: "interview" as const,
  vendor: "claude" as const,
  model: "opus",
  effort: "high" as const
};

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

function render(status: IntentStatus) {
  return renderWithQuery(
    <AgentTerminalPanel intentId="intent-1" intentStatus={status} />,
    { withBridge: false }
  );
}

describe("AgentTerminalPanel «Начать работу»", () => {
  beforeEach(() => {
    getIntentTerminalSession.mockReset();
    runIntentTerminal.mockReset();
    killIntentTerminal.mockReset();
    listIntentRepositories.mockReset();
    listIntentRepositories.mockResolvedValue([]);
    runIntentTerminal.mockResolvedValue(
      sessionResponse("running", { ...INTERVIEW_LAUNCH, mode: "work" })
    );
    killIntentTerminal.mockResolvedValue(sessionResponse("exited"));
  });

  afterEach(() => {
    cleanup();
  });

  it("показывает кнопку при live-интервью в статусе «Жду ответа»", async () => {
    getIntentTerminalSession.mockResolvedValue(
      sessionResponse("running", INTERVIEW_LAUNCH)
    );
    render("awaiting_operator");
    await screen.findByTestId("agent-terminal-live-badge");
    expect(screen.getByTestId("agent-terminal-start-work")).toBeTruthy();
  });

  it("прячет кнопку, когда статус не «Жду ответа»", async () => {
    getIntentTerminalSession.mockResolvedValue(
      sessionResponse("running", INTERVIEW_LAUNCH)
    );
    render("interview");
    await screen.findByTestId("agent-terminal-live-badge");
    expect(screen.queryByTestId("agent-terminal-start-work")).toBeNull();
  });

  it("прячет кнопку, когда живой режим не интервью", async () => {
    getIntentTerminalSession.mockResolvedValue(
      sessionResponse("running", { ...INTERVIEW_LAUNCH, mode: "work" })
    );
    render("awaiting_operator");
    await screen.findByTestId("agent-terminal-live-badge");
    expect(screen.queryByTestId("agent-terminal-start-work")).toBeNull();
  });

  it("прячет кнопку без живой сессии", async () => {
    getIntentTerminalSession.mockResolvedValue(sessionResponse("exited"));
    render("awaiting_operator");
    await screen.findByTestId("agent-terminal-run");
    expect(screen.queryByTestId("agent-terminal-start-work")).toBeNull();
  });

  it("открывает модалку с предустановленным режимом «Работа»", async () => {
    getIntentTerminalSession.mockResolvedValue(
      sessionResponse("running", INTERVIEW_LAUNCH)
    );
    render("awaiting_operator");

    fireEvent.click(await screen.findByTestId("agent-terminal-start-work"));

    const modeSelect = await screen.findByTestId<HTMLSelectElement>(
      "agent-terminal-mode"
    );
    expect(modeSelect.value).toBe("work");
  });

  it("по «Запустить» убивает интервью-сессию и поднимает новую в режиме «Работа»", async () => {
    getIntentTerminalSession.mockResolvedValue(
      sessionResponse("running", INTERVIEW_LAUNCH)
    );
    render("awaiting_operator");

    fireEvent.click(await screen.findByTestId("agent-terminal-start-work"));

    const launch = await screen.findByTestId<HTMLButtonElement>(
      "agent-terminal-preflight-launch"
    );
    await waitFor(() => {
      expect(launch.disabled).toBe(false);
    });
    fireEvent.click(launch);

    await waitFor(() => {
      expect(runIntentTerminal).toHaveBeenCalledTimes(1);
    });
    // Перезапуск = kill текущей сессии перед запуском новой.
    expect(killIntentTerminal).toHaveBeenCalledTimes(1);
    expect(killIntentTerminal.mock.invocationCallOrder[0]).toBeLessThan(
      runIntentTerminal.mock.invocationCallOrder[0]
    );
    expect(runIntentTerminal.mock.calls[0][1].launch.mode).toBe("work");
  });

  it("по «Отмена» оставляет интервью-сессию живой без kill", async () => {
    getIntentTerminalSession.mockResolvedValue(
      sessionResponse("running", INTERVIEW_LAUNCH)
    );
    render("awaiting_operator");

    fireEvent.click(await screen.findByTestId("agent-terminal-start-work"));
    await screen.findByTestId("agent-terminal-mode");
    fireEvent.click(screen.getByText("Отмена"));

    await waitFor(() => {
      expect(screen.queryByTestId("agent-terminal-mode")).toBeNull();
    });
    expect(killIntentTerminal).not.toHaveBeenCalled();
    expect(runIntentTerminal).not.toHaveBeenCalled();
    expect(screen.getByTestId("agent-terminal-kill")).toBeTruthy();
  });
});
