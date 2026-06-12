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

function preview(selected: string[] | null): IntentTerminalPreviewResponse {
  const p1Selected = selected?.includes("p1") ?? false;
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
        selected: p1Selected,
        text: "postgres rule"
      }
    ],
    selected_part_ids: p1Selected ? ["m1", "p1"] : ["m1"],
    system_prompt: p1Selected
      ? "mandatory text\n\npostgres rule"
      : "mandatory text",
    user_prompt: "BODY"
  };
}

function renderModal(onLaunch: (p: TerminalRunPayload) => void) {
  return render(
    <PreflightModal
      open
      intentId="intent-1"
      launch={LAUNCH}
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
    previewIntentTerminal.mockImplementation((_id, _mode, selected) =>
      Promise.resolve(preview(selected))
    );
  });

  afterEach(() => {
    cleanup();
  });

  it("переключение опциональной части перезапрашивает preview и payload несёт выбранные id", async () => {
    const onLaunch = vi.fn();
    renderModal(onLaunch);

    await waitFor(() => {
      expect(previewIntentTerminal).toHaveBeenCalledWith(
        "intent-1",
        "work",
        null
      );
    });

    fireEvent.click(screen.getByRole("checkbox", { name: "Часть postgres" }));

    await waitFor(() => {
      expect(previewIntentTerminal).toHaveBeenCalledWith("intent-1", "work", [
        "p1"
      ]);
    });

    fireEvent.click(screen.getByTestId("agent-terminal-preflight-launch"));

    expect(onLaunch).toHaveBeenCalledTimes(1);
    const payload = onLaunch.mock.calls[0][0] as TerminalRunPayload;
    expect(payload.selectedPartIds).toEqual(["p1"]);
    expect(payload.systemPrompt).toBe("mandatory text\n\npostgres rule");
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
});
