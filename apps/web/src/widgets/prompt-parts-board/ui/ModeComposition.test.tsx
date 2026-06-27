import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type {
  PromptPartListItem,
  PromptPartModeRole
} from "@/entities/prompt-part";

vi.mock("./RoleSelect", () => ({
  RoleSelect: () => <div data-testid="role-select" />
}));

import { ModeComposition } from "./ModeComposition";

function part(
  id: string,
  scope: "system" | "user",
  role: PromptPartModeRole["role"]
): PromptPartListItem {
  return {
    id,
    key: id,
    scope,
    text_short: "preview",
    current_version: 1,
    mode_roles: [{ mode: "work", role, order: 0 }],
    created_at: "2026-01-01T00:00:00Z",
    updated_at: "2026-01-01T00:00:00Z"
  };
}

const parts = [
  part("common", "system", "mandatory"),
  part("commit", "user", "default_on"),
  part("extra", "user", "default_off"),
  { ...part("hidden", "user", "default_on"), mode_roles: [] }
];

const noop = () => {
  /* test callback */
};

afterEach(() => {
  cleanup();
});

describe("ModeComposition", () => {
  it("раскладывает блоки выбранного режима по корзинам", () => {
    render(
      <ModeComposition
        parts={parts}
        mode="work"
        onOpenPart={noop}
        onCreatePart={noop}
      />
    );

    expect(screen.getByText(/Входит в «Работа»/)).toBeTruthy();
    expect(screen.getByText("common")).toBeTruthy(); // included
    expect(screen.getByText("commit")).toBeTruthy(); // included
    expect(screen.getByText("extra")).toBeTruthy(); // available
    // excluded bucket is collapsed → its block is not mounted
    expect(screen.queryByText("hidden")).toBeNull();
    expect(screen.getByText("Не входит в этот режим")).toBeTruthy();
  });

  it("«Создать блок» зовёт onCreatePart, клик по блоку — onOpenPart", () => {
    const onCreate = vi.fn();
    const onOpen = vi.fn();
    render(
      <ModeComposition
        parts={parts}
        mode="work"
        onOpenPart={onOpen}
        onCreatePart={onCreate}
      />
    );

    fireEvent.click(screen.getByText("Создать блок"));
    expect(onCreate).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByText("commit"));
    expect(onOpen).toHaveBeenCalledTimes(1);
  });
});
