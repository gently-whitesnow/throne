import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import type { PromptPartListItem } from "@/entities/prompt-part";
import {
  PromptPartDetailDialog,
  type PromptPartDialogTarget
} from "./PromptPartDetailDialog";

const { httpGet, httpPost, httpDelete, HttpErrorMock } = vi.hoisted(() => {
  class HttpErrorMock extends Error {
    status: number;
    constructor(status: number) {
      super(`HTTP ${String(status)}`);
      this.status = status;
    }
  }
  return {
    httpGet: vi.fn(),
    httpPost: vi.fn(),
    httpDelete: vi.fn(),
    HttpErrorMock
  };
});

vi.mock("@/shared/api", () => ({
  HttpError: HttpErrorMock,
  httpGet,
  httpPost,
  httpPut: vi.fn(),
  httpDelete,
  promptPartsEndpoints: {
    listPromptParts: () => "/prompt-parts",
    createPromptPart: () => "/prompt-parts",
    getPromptPart: (id: string) => `/prompt-parts/${id}`,
    deletePromptPart: (id: string) => `/prompt-parts/${id}`,
    replacePromptPartText: (id: string) => `/prompt-parts/${id}/replace-text`,
    setPromptPartRoles: (id: string) => `/prompt-parts/${id}/roles`
  }
}));

const render = (ui: React.ReactElement) =>
  renderWithQuery(ui, { withBridge: false });

const noop = () => {
  /* dialog onClose no-op */
};

function listItem(): PromptPartListItem {
  return {
    id: "p1",
    key: "my-part",
    scope: "user",
    description: "desc",
    text_short: "short text",
    current_version: 3,
    mode_roles: [],
    created_at: "2026-01-01T00:00:00Z",
    updated_at: "2026-01-01T00:00:00Z"
  };
}

const editTarget: PromptPartDialogTarget = {
  mode: "detail",
  part: listItem()
};

function systemListItem(): PromptPartListItem {
  return {
    id: "sys-common",
    key: "common",
    scope: "system",
    text_short: "SYS",
    current_version: 1,
    mode_roles: [{ mode: "work", role: "mandatory", order: 0 }],
    created_at: "2026-01-01T00:00:00Z",
    updated_at: "2026-01-01T00:00:00Z"
  };
}

afterEach(() => {
  cleanup();
  httpGet.mockReset();
  httpPost.mockReset();
  httpDelete.mockReset();
});

describe("PromptPartDetailDialog — создание", () => {
  it("сабмит вызывает POST /prompt-parts с key и text", async () => {
    httpPost.mockResolvedValue({ id: "new" });

    render(
      <PromptPartDetailDialog target={{ mode: "create" }} onClose={noop} />
    );

    fireEvent.change(screen.getByLabelText("Key части"), {
      target: { value: "fresh-key" }
    });
    fireEvent.change(screen.getByLabelText("Текст части"), {
      target: { value: "hello" }
    });
    fireEvent.click(screen.getByRole("button", { name: "Создать" }));

    await waitFor(() => {
      expect(httpPost).toHaveBeenCalledWith(
        "/prompt-parts",
        { key: "fresh-key", text: "hello", description: null },
        undefined
      );
    });
  });
});

describe("PromptPartDetailDialog — редактирование", () => {
  it("сохранение текста вызывает replace-text", async () => {
    httpGet.mockResolvedValue({
      id: "p1",
      key: "my-part",
      scope: "user",
      text: "old body",
      current_version: 3,
      mode_roles: [],
      created_at: "2026-01-01T00:00:00Z",
      updated_at: "2026-01-01T00:00:00Z"
    });
    httpPost.mockResolvedValue({ id: "p1" });

    render(<PromptPartDetailDialog target={editTarget} onClose={noop} />);

    const textarea = await screen.findByLabelText("Текст части");
    fireEvent.change(textarea, { target: { value: "new body" } });
    fireEvent.click(screen.getByRole("button", { name: "Сохранить текст" }));

    await waitFor(() => {
      expect(httpPost).toHaveBeenCalledWith(
        "/prompt-parts/p1/replace-text",
        expect.objectContaining({ expected_version: 3 }),
        undefined
      );
    });
  });

  it("удаление вызывает DELETE", async () => {
    httpGet.mockResolvedValue({
      id: "p1",
      key: "my-part",
      scope: "user",
      text: "body",
      current_version: 3,
      mode_roles: [],
      created_at: "2026-01-01T00:00:00Z",
      updated_at: "2026-01-01T00:00:00Z"
    });
    httpDelete.mockResolvedValue(undefined);

    render(<PromptPartDetailDialog target={editTarget} onClose={noop} />);

    fireEvent.click(screen.getByRole("button", { name: "Удалить" }));
    fireEvent.click(screen.getByRole("button", { name: "Да, удалить" }));

    await waitFor(() => {
      expect(httpDelete).toHaveBeenCalledWith("/prompt-parts/p1", undefined);
    });
  });

  it("system-часть открывается read-only: текст из API, без сохранения и удаления", async () => {
    httpGet.mockResolvedValue({
      id: "sys-common",
      key: "common",
      scope: "system",
      text: "SYSTEM BODY",
      current_version: 1,
      mode_roles: [{ mode: "work", role: "mandatory", order: 0 }],
      created_at: "2026-01-01T00:00:00Z",
      updated_at: "2026-01-01T00:00:00Z"
    });

    render(
      <PromptPartDetailDialog
        target={{ mode: "detail", part: systemListItem() }}
        onClose={noop}
      />
    );

    await waitFor(() => {
      expect(screen.getByText("SYSTEM BODY")).toBeTruthy();
    });
    expect(screen.queryByLabelText("Текст части")).toBeNull();
    expect(
      screen.queryByRole("button", { name: "Сохранить текст" })
    ).toBeNull();
    expect(screen.queryByRole("button", { name: "Удалить" })).toBeNull();
  });

  it("удаление при 409 показывает сообщение про снятие ролей", async () => {
    httpGet.mockResolvedValue({
      id: "p1",
      key: "my-part",
      scope: "user",
      text: "body",
      current_version: 3,
      mode_roles: [],
      created_at: "2026-01-01T00:00:00Z",
      updated_at: "2026-01-01T00:00:00Z"
    });
    httpDelete.mockRejectedValue(new HttpErrorMock(409));

    render(<PromptPartDetailDialog target={editTarget} onClose={noop} />);

    fireEvent.click(screen.getByRole("button", { name: "Удалить" }));
    fireEvent.click(screen.getByRole("button", { name: "Да, удалить" }));

    await waitFor(() => {
      expect(
        screen.getByText(/Сначала снимите все роли части во всех режимах/)
      ).toBeTruthy();
    });
  });
});
