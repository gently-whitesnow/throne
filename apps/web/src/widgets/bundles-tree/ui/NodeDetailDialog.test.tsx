import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { BundleEntryNode, BundleNode, SelectedNode } from "../model/types";
import { NodeDetailDialog } from "./NodeDetailDialog";

const { httpPost } = vi.hoisted(() => ({ httpPost: vi.fn() }));
const noop = () => {
  // intentional no-op handler for dialog callbacks under test
};

vi.mock("@/shared/api", () => ({
  HttpError: class HttpError extends Error {
    status = 500;
  },
  httpPost,
  instructionsEndpoints: {
    createInstruction: () => "/instructions",
    replaceInstructionText: (id: string) => `/instructions/${id}/replace-text`
  }
}));

function makeEntry(present: boolean): BundleEntryNode {
  return {
    scope: "user",
    kind: "work",
    instruction_id: present ? "iid-1" : null,
    current_version: present ? 3 : 0,
    text: present ? "existing text" : "",
    editable: true,
    present
  };
}

function makeSelection(entry: BundleEntryNode): SelectedNode {
  const bundle: BundleNode = { mode: "work", includes: [entry] };
  return { kind: "entry", bundle, entry };
}

describe("NodeDetailDialog: editable user-entry без записи", () => {
  afterEach(() => {
    cleanup();
    httpPost.mockReset();
  });

  it("показывает кнопку «Создать инструкцию» вместо текстовой заглушки", () => {
    render(
      <NodeDetailDialog
        selection={makeSelection(makeEntry(false))}
        onClose={noop}
        onSaved={noop}
      />
    );

    expect(
      screen.getByRole("button", { name: "Создать инструкцию" })
    ).toBeTruthy();
  });

  it("клик «Создать инструкцию» открывает форму с пустым textarea", () => {
    render(
      <NodeDetailDialog
        selection={makeSelection(makeEntry(false))}
        onClose={noop}
        onSaved={noop}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Создать инструкцию" }));

    const textarea = screen.getByRole<HTMLTextAreaElement>("textbox", {
      name: "Текст instruction"
    });
    expect(textarea.value).toBe("");
    expect(screen.getByRole("button", { name: "Создать" })).toBeTruthy();
  });

  it("сабмит формы дёргает POST /instructions с kind и текстом", async () => {
    httpPost.mockResolvedValue({
      id: "new-iid",
      kind: "work",
      current_version: 1,
      text: "draft",
      created_at: "2026-05-15T00:00:00Z",
      updated_at: "2026-05-15T00:00:00Z"
    });
    const onSaved = vi.fn();

    render(
      <NodeDetailDialog
        selection={makeSelection(makeEntry(false))}
        onClose={noop}
        onSaved={onSaved}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Создать инструкцию" }));
    const textarea = screen.getByRole("textbox", {
      name: "Текст instruction"
    });
    fireEvent.change(textarea, { target: { value: "draft" } });
    fireEvent.click(screen.getByRole("button", { name: "Создать" }));

    await vi.waitFor(() => {
      expect(httpPost).toHaveBeenCalledWith("/instructions", {
        kind: "work",
        text: "draft"
      });
    });
    await vi.waitFor(() => {
      expect(onSaved).toHaveBeenCalled();
    });
  });
});
