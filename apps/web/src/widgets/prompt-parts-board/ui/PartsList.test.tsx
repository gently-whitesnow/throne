import { cleanup, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import type { PromptPartListItem } from "@/entities/prompt-part";

import { PartsList } from "./PartsList";

vi.mock("@/shared/api", () => ({
  HttpError: class HttpError extends Error {
    status = 500;
  },
  httpPut: vi.fn(),
  promptPartsEndpoints: {
    setPromptPartRoles: (id: string) => `/prompt-parts/${id}/roles`
  }
}));

const render = (ui: React.ReactElement) =>
  renderWithQuery(ui, { withBridge: false });

const noop = () => {
  /* row callback no-op */
};

function systemPart(): PromptPartListItem {
  return {
    id: "sys-common",
    key: "common",
    scope: "system",
    text_short: "SYS",
    current_version: 1,
    mode_roles: [
      { mode: "work", role: "mandatory", order: 0 },
      { mode: "interview", role: "mandatory", order: 0 }
    ],
    created_at: "2026-01-01T00:00:00Z",
    updated_at: "2026-01-01T00:00:00Z"
  };
}

function userPart(): PromptPartListItem {
  return {
    id: "p1",
    key: "opt-one",
    scope: "user",
    text_short: "preview",
    current_version: 1,
    mode_roles: [{ mode: "work", role: "default_on", order: 0 }],
    created_at: "2026-01-01T00:00:00Z",
    updated_at: "2026-01-01T00:00:00Z"
  };
}

afterEach(() => {
  cleanup();
});

describe("PartsList", () => {
  it("system-часть отрендерена read-only, без select управления ролями", () => {
    render(
      <PartsList parts={[systemPart()]} onOpenPart={noop} onCreatePart={noop} />
    );

    const sysRow = screen.getByText("common").closest("li");
    expect(sysRow).not.toBeNull();
    expect(within(sysRow as HTMLElement).queryByRole("combobox")).toBeNull();
    expect(
      within(sysRow as HTMLElement).getByText("из манифеста")
    ).toBeTruthy();
  });

  it("user-часть отрендерена с управлением ролями (select per mode)", () => {
    render(
      <PartsList parts={[userPart()]} onOpenPart={noop} onCreatePart={noop} />
    );

    const optRow = screen.getByText("opt-one").closest("li");
    expect(optRow).not.toBeNull();
    // one select per embedded mode (work / interview / free)
    expect(within(optRow as HTMLElement).getAllByRole("combobox")).toHaveLength(
      3
    );
  });
});
