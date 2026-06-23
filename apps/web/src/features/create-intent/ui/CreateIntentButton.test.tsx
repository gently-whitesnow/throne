import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { CreateIntentButton } from "./CreateIntentButton";

vi.mock("@/shared/api", () => ({
  HttpError: class HttpError extends Error {
    status = 500;
  },
  INTENT_ATTACHMENTS_CHANGED_EVENT: "intent-attachments-changed",
  httpPost: vi.fn(),
  httpPostForm: vi.fn(),
  intentsEndpoints: {
    createIntent: () => "/api/intents",
    uploadIntentAttachment: (intentId: string) =>
      `/api/intents/${intentId}/attachments`
  }
}));

vi.mock("@/entities/tag", () => ({
  useTagPicker: () => ({
    query: "",
    setQuery: vi.fn(),
    candidates: [],
    loadError: null,
    createTag: vi.fn().mockResolvedValue("")
  })
}));

describe("CreateIntentButton", () => {
  afterEach(() => {
    cleanup();
  });

  it("opens creation form in a centered dialog", () => {
    render(<CreateIntentButton />);

    fireEvent.click(screen.getByRole("button", { name: "Создать intent" }));

    expect(
      screen.getByRole("dialog", { name: "Сформулируйте задачу в одном окне" })
    ).toBeTruthy();
  });

  it("closes the dialog when escape is pressed", () => {
    render(<CreateIntentButton />);

    fireEvent.click(screen.getByRole("button", { name: "Создать intent" }));
    fireEvent.keyDown(window, { key: "Escape" });

    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("preselects initialTags as chips when opening the dialog", () => {
    render(<CreateIntentButton initialTags={["throne"]} />);

    fireEvent.click(screen.getByRole("button", { name: "Создать intent" }));

    expect(
      screen.getByRole("button", { name: "Убрать тег throne" })
    ).toBeTruthy();
  });

  it("closes the dialog when clicking outside", () => {
    render(<CreateIntentButton />);

    fireEvent.click(screen.getByRole("button", { name: "Создать intent" }));

    const overlay = document.querySelector(".modal");
    if (!overlay) {
      throw new Error("Expected modal overlay to exist");
    }

    fireEvent.click(overlay);

    expect(screen.queryByRole("dialog")).toBeNull();
  });
});
