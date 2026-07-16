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

vi.mock("@/shared/realtime", () => ({
  useRealtimeEvent: vi.fn()
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
        models: ["gpt-5.6-sol", "gpt-5.6-terra", "gpt-5.6-luna", "gpt-5.5"],
        default_model: "gpt-5.6-sol",
        efforts: ["low", "medium", "high", "xhigh"],
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

function previewResponse(): IntentTerminalPreviewResponse {
  return {
    intent_id: "intent-1",
    intent_version: 2,
    mode: "free",
    parts: [
      {
        part_id: "m1",
        key: "common",
        scope: "system",
        role: "mandatory",
        order: 0,
        editable: false,
        present: true,
        selected: true,
        text: "RULES"
      }
    ],
    available_skills_for_mode: [],
    selected_part_ids: ["m1"],
    system_prompt: "RULES",
    user_prompt: "BODY",
    workspace_map: "=== Карта workspace ==="
  };
}

const render = () =>
  renderWithQuery(
    <AgentTerminalPanel intentId="intent-1" intentStatus="work" />,
    {
      withBridge: false
    }
  );

describe("AgentTerminalPanel", () => {
  beforeEach(() => {
    getIntentTerminalSession.mockReset();
    runIntentTerminal.mockReset();
    previewIntentTerminal.mockReset();
    previewIntentTerminal.mockResolvedValue(previewResponse());
    listIntentRepositories.mockReset();
    listIntentRepositories.mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
  });

  it("Run открывает модалку с осью запуска, а /run уходит только после подтверждения с собранным payload", async () => {
    getIntentTerminalSession.mockResolvedValue(sessionResponse("exited"));
    previewIntentTerminal.mockResolvedValue(previewResponse());
    runIntentTerminal.mockResolvedValue(sessionResponse("running"));

    render();

    const run =
      await screen.findByTestId<HTMLButtonElement>("agent-terminal-run");
    await waitFor(() => {
      expect(run.disabled).toBe(false);
    });
    fireEvent.click(run);
    expect(runIntentTerminal).not.toHaveBeenCalled();

    // Селекторы оси теперь живут в модалке, рядом с кнопкой запуска.
    const vendor = await screen.findByTestId<HTMLSelectElement>(
      "agent-terminal-vendor"
    );
    await waitFor(() => {
      expect(vendor.disabled).toBe(false);
    });
    fireEvent.change(vendor, { target: { value: "codex" } });

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
        model: "gpt-5.6-sol",
        effort: "high"
      },
      reviewBindingId: null,
      selectedPartIds: [],
      selectedSkillIds: [],
      systemPrompt: "RULES",
      userPrompt: "BODY",
      intentTextUpdate: null
    });
  });

  it("без живой сессии префиллит ось модалки из last-used интента, а не из дефолта каталога", async () => {
    getIntentTerminalSession.mockResolvedValue(
      sessionResponse("exited", {
        mode: "work",
        vendor: "codex",
        model: "gpt-5.6-terra",
        effort: "high"
      })
    );

    render();

    const run =
      await screen.findByTestId<HTMLButtonElement>("agent-terminal-run");
    await waitFor(() => {
      expect(run.disabled).toBe(false);
    });
    fireEvent.click(run);

    const vendor = await screen.findByTestId<HTMLSelectElement>(
      "agent-terminal-vendor"
    );
    const model = screen.getByTestId<HTMLSelectElement>("agent-terminal-model");

    await waitFor(() => {
      expect(vendor.value).toBe("codex");
    });
    expect(model.value).toBe("gpt-5.6-terra");
    expect(vendor.disabled).toBe(false);
  });
});
