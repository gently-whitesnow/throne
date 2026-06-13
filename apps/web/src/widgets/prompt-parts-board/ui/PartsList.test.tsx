import { cleanup, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import type { PromptPartListItem } from "@/entities/prompt-part";

import type { ProjectedInstruction } from "../model/composition";
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

function systemInstruction(): ProjectedInstruction {
  return {
    scope: "system",
    key: "common",
    prompt_part_id: null,
    currentVersion: 1,
    text: "SYS",
    editable: false,
    present: true,
    modes: ["work", "interview"]
  };
}

function optional(): PromptPartListItem {
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
  it("system-инструкция отрендерена read-only с [must], без select", () => {
    render(
      <PartsList
        instructions={[systemInstruction()]}
        optionalParts={[]}
        onOpenInstruction={noop}
        onOpenOptional={noop}
        onCreateOptional={noop}
      />
    );

    const sysRow = screen.getByText("common").closest("li");
    expect(sysRow).not.toBeNull();
    expect(within(sysRow as HTMLElement).getByText("must")).toBeTruthy();
    expect(within(sysRow as HTMLElement).queryByRole("combobox")).toBeNull();
  });

  it("optional-часть отрендерена с управлением ролями (select per mode)", () => {
    render(
      <PartsList
        instructions={[]}
        optionalParts={[optional()]}
        onOpenInstruction={noop}
        onOpenOptional={noop}
        onCreateOptional={noop}
      />
    );

    const optRow = screen.getByText("opt-one").closest("li");
    expect(optRow).not.toBeNull();
    // one select per mode (work / interview / free)
    expect(within(optRow as HTMLElement).getAllByRole("combobox")).toHaveLength(
      3
    );
  });
});
