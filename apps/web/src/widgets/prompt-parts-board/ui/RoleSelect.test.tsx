import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import type { PromptPartListItem } from "@/entities/prompt-part";
import { RoleSelect } from "./RoleSelect";

const { httpPut } = vi.hoisted(() => ({
  httpPut:
    vi.fn<
      (
        path: string,
        body: { mode_roles: unknown[] },
        signal?: AbortSignal
      ) => unknown
    >()
}));

vi.mock("@/shared/api", () => ({
  HttpError: class HttpError extends Error {
    status = 500;
  },
  httpGet: vi.fn(),
  httpPost: vi.fn(),
  httpPut,
  httpDelete: vi.fn(),
  promptPartsEndpoints: {
    setPromptPartRoles: (id: string) => `/prompt-parts/${id}/roles`
  }
}));

const render = (ui: React.ReactElement) =>
  renderWithQuery(ui, { withBridge: false });

function part(): PromptPartListItem {
  return {
    id: "p1",
    key: "k",
    scope: "user",
    text_short: "s",
    current_version: 1,
    mode_roles: [
      { mode: "interview", role: "mandatory", order: 0 },
      { mode: "work", role: "default_off", order: 2 }
    ],
    created_at: "2026-01-01T00:00:00Z",
    updated_at: "2026-01-01T00:00:00Z"
  };
}

afterEach(() => {
  cleanup();
  httpPut.mockReset();
});

describe("RoleSelect", () => {
  it("смена роли в режиме шлёт whole-replace, сохраняя другие режимы", async () => {
    httpPut.mockResolvedValue({ id: "p1", mode_roles: [] });

    render(<RoleSelect part={part()} mode="work" orderForNew={5} />);

    fireEvent.change(screen.getByRole("combobox"), {
      target: { value: "mandatory" }
    });

    await waitFor(() => {
      const arrayContaining: unknown = expect.arrayContaining([
        { mode: "interview", role: "mandatory", order: 0 },
        { mode: "work", role: "mandatory", order: 2 }
      ]);
      expect(httpPut).toHaveBeenCalledWith(
        "/prompt-parts/p1/roles",
        { mode_roles: arrayContaining },
        undefined
      );
    });
    expect(httpPut.mock.calls[0][1].mode_roles).toHaveLength(2);
  });

  it("выбор none снимает роль режима, сохраняя остальные", async () => {
    httpPut.mockResolvedValue({ id: "p1", mode_roles: [] });

    render(<RoleSelect part={part()} mode="work" orderForNew={5} />);

    fireEvent.change(screen.getByRole("combobox"), {
      target: { value: "none" }
    });

    await waitFor(() => {
      expect(httpPut).toHaveBeenCalledWith(
        "/prompt-parts/p1/roles",
        { mode_roles: [{ mode: "interview", role: "mandatory", order: 0 }] },
        undefined
      );
    });
  });
});
