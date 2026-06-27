import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type {
  IntentTerminalPreviewResponse,
  TerminalLaunchArgs,
  TerminalRunPayload
} from "../model/types";

import { PreflightModal } from "./PreflightModal";

const previewIntentTerminal =
  vi.fn<
    (
      intentId: string,
      mode: string,
      selectedPartIds: string[] | null
    ) => Promise<IntentTerminalPreviewResponse>
  >();

vi.mock("../api/agent-terminal-api", () => ({
  previewIntentTerminal: (
    intentId: string,
    mode: string,
    selectedPartIds: string[] | null
  ) => previewIntentTerminal(intentId, mode, selectedPartIds)
}));

const LAUNCH: TerminalLaunchArgs = {
  mode: "work",
  vendor: "claude",
  model: "opus",
  effort: "high"
};

function preview(): IntentTerminalPreviewResponse {
  return {
    intent_id: "intent-1",
    intent_version: 2,
    mode: "work",
    parts: [],
    available_skills_for_mode: [
      {
        skill_id: "intent",
        source: "throne",
        title: "Intent",
        description: "Правка Intent.text и чтение связанных интентов.",
        materializable: true,
        reason: null,
        default_enabled: false,
        selected: false
      }
    ],
    selected_part_ids: [],
    system_prompt: "",
    user_prompt: "BODY",
    workspace_map:
      "=== Карта workspace ===\nТеги интента: throne\nСвязи:\n- заблокирован intent_id=peer-1 (без причины связи)\n- ведёт к intent_id=peer-2: soft context\n======================="
  };
}

function renderModal(onLaunch: (p: TerminalRunPayload) => void = vi.fn()) {
  return render(
    <PreflightModal
      open
      intentId="intent-1"
      launch={LAUNCH}
      reviewBindingId={null}
      actionLabel="Запустить"
      isSubmitting={false}
      onClose={() => undefined}
      onLaunch={onLaunch}
    />
  );
}

describe("PreflightModal workspace map context", () => {
  beforeEach(() => {
    previewIntentTerminal.mockReset();
    previewIntentTerminal.mockResolvedValue(preview());
  });

  afterEach(() => {
    cleanup();
  });

  it("показывает workspace map отдельным read-only блоком, не смешивая его с телом интента", async () => {
    renderModal();

    const workspaceMap = await screen.findByTestId(
      "agent-terminal-workspace-map-context"
    );
    expect(workspaceMap.textContent).toContain("Карта workspace");
    expect(workspaceMap.textContent).toContain("Связи:");
    expect(workspaceMap.textContent).toContain("intent_id=peer-1");
    expect(workspaceMap.textContent).toContain("soft context");
    const skillsTitle = screen.getByText("Скилы сессии");
    expect(
      skillsTitle.compareDocumentPosition(workspaceMap) &
        Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy();

    await waitFor(() => {
      expect(
        screen.getByTestId<HTMLTextAreaElement>("agent-terminal-task-body")
          .value
      ).toBe("BODY");
    });
  });
});
