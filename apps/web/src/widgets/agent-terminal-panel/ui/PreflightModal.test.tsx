import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor
} from "@testing-library/react";
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
        text: "mandatory text"
      },
      {
        part_id: "p1",
        key: "postgres",
        scope: "system",
        role: "default_off",
        order: 1,
        editable: true,
        present: true,
        selected: false,
        text: "postgres rule"
      }
    ],
    available_skills_for_mode: [],
    selected_part_ids: ["m1"],
    system_prompt: "mandatory text",
    user_prompt: "BODY"
  };
}

function renderModal(
  onLaunch: (p: TerminalRunPayload) => void,
  reviewBindingId: string | null = null
) {
  return render(
    <PreflightModal
      open
      intentId="intent-1"
      launch={LAUNCH}
      reviewBindingId={reviewBindingId}
      actionLabel="Запустить"
      isSubmitting={false}
      onClose={() => undefined}
      onLaunch={onLaunch}
    />
  );
}

describe("PreflightModal", () => {
  beforeEach(() => {
    previewIntentTerminal.mockReset();
    previewIntentTerminal.mockImplementation(() => Promise.resolve(preview()));
  });

  afterEach(() => {
    cleanup();
  });

  it("preview тянется один раз; включение части пересобирает system-блок локально без перезапроса", async () => {
    const onLaunch = vi.fn();
    renderModal(onLaunch);

    await waitFor(() => {
      expect(previewIntentTerminal).toHaveBeenCalledWith(
        "intent-1",
        "work",
        null
      );
    });

    // выключенная часть — чипом; клик включает её в своей рамке
    fireEvent.click(screen.getByText("+ postgres"));
    expect(screen.getByLabelText("Текст части postgres")).toBeTruthy();

    fireEvent.click(screen.getByTestId("agent-terminal-preflight-launch"));

    expect(previewIntentTerminal).toHaveBeenCalledTimes(1);
    const payload = onLaunch.mock.calls[0][0] as TerminalRunPayload;
    expect(payload.selectedPartIds).toEqual(["p1"]);
    expect(payload.selectedSkillIds).toEqual([]);
    expect(payload.reviewBindingId).toBeNull();
    expect(payload.systemPrompt).toBe("mandatory text\n\npostgres rule");
  });

  it("выбранные скилы попадают в payload, недоступный скил остаётся выключенным", async () => {
    previewIntentTerminal.mockResolvedValueOnce({
      ...preview(),
      available_skills_for_mode: [
        {
          skill_id: "throne-intent-ops",
          source: "throne",
          title: "Intent operations",
          description: "Правка Intent.text",
          materializable: true,
          reason: null,
          default_enabled: false,
          selected: false
        },
        {
          skill_id: "throne-review-artifact",
          source: "throne",
          title: "Review artifact",
          description: "Запись review_recommendation",
          materializable: false,
          reason: "нет привязанного репозитория",
          default_enabled: true,
          selected: false
        }
      ]
    });
    const onLaunch = vi.fn();
    renderModal(onLaunch);

    const intentOps = await screen.findByLabelText("Intent operations");
    const reviewArtifact =
      screen.getByLabelText<HTMLInputElement>("Review artifact");
    fireEvent.click(intentOps);
    fireEvent.click(reviewArtifact);
    fireEvent.click(screen.getByTestId("agent-terminal-preflight-launch"));

    const payload = onLaunch.mock.calls[0][0] as TerminalRunPayload;
    expect(payload.selectedSkillIds).toEqual(["throne-intent-ops"]);
    expect(reviewArtifact.disabled).toBe(true);
    expect(screen.getByText("нет привязанного репозитория")).toBeTruthy();
  });

  it("правка тела части — session override, попадает в собранный system-блок", async () => {
    const onLaunch = vi.fn();
    renderModal(onLaunch);

    const mandatory = await screen.findByLabelText("Текст части common");
    fireEvent.change(mandatory, { target: { value: "mandatory edited" } });

    fireEvent.click(screen.getByTestId("agent-terminal-preflight-launch"));
    const payload = onLaunch.mock.calls[0][0] as TerminalRunPayload;
    expect(payload.systemPrompt).toBe("mandatory edited");
  });

  it("правка тела включает чекбокс сохранения и кладёт intent_text_update с expected_version", async () => {
    const onLaunch = vi.fn();
    renderModal(onLaunch);

    const body = await screen.findByTestId("agent-terminal-task-body");
    const save = screen.getByTestId<HTMLInputElement>(
      "agent-terminal-save-intent"
    );
    expect(save.checked).toBe(false);

    fireEvent.change(body, { target: { value: "BODY edited" } });
    expect(save.checked).toBe(true);

    fireEvent.click(screen.getByTestId("agent-terminal-preflight-launch"));

    const payload = onLaunch.mock.calls[0][0] as TerminalRunPayload;
    expect(payload.userPrompt).toBe("BODY edited");
    expect(payload.intentTextUpdate).toEqual({
      expected_version: 2,
      old_text: "BODY",
      new_text: "BODY edited"
    });
  });

  it("без правки тела сохранение выключено и intent_text_update пуст", async () => {
    const onLaunch = vi.fn();
    renderModal(onLaunch);

    await screen.findByTestId("agent-terminal-task-body");
    fireEvent.click(screen.getByTestId("agent-terminal-preflight-launch"));

    const payload = onLaunch.mock.calls[0][0] as TerminalRunPayload;
    expect(payload.intentTextUpdate).toBeNull();
  });

  it("прокидывает выбранный review binding в payload", async () => {
    const onLaunch = vi.fn();
    renderModal(onLaunch, "binding-2");

    await screen.findByTestId("agent-terminal-task-body");
    fireEvent.click(screen.getByTestId("agent-terminal-preflight-launch"));

    const payload = onLaunch.mock.calls[0][0] as TerminalRunPayload;
    expect(payload.reviewBindingId).toBe("binding-2");
  });
});
