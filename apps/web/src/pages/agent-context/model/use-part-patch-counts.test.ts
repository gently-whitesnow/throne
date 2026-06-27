import { renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  usePromptPartPatchesList: vi.fn<() => { data: unknown }>()
}));

vi.mock("@/entities/prompt-part-patch", () => ({
  usePromptPartPatchesList: () => mocks.usePromptPartPatchesList()
}));

import { partPatchKey, usePartPatchCounts } from "./use-part-patch-counts";

describe("usePartPatchCounts", () => {
  it("partPatchKey склеивает scope/key", () => {
    expect(partPatchKey("user", "commit")).toBe("user/commit");
  });

  it("считает proposed-патчи по целевой части", () => {
    mocks.usePromptPartPatchesList.mockReturnValue({
      data: {
        items: [
          { target_scope: "user", target_key: "commit" },
          { target_scope: "user", target_key: "commit" },
          { target_scope: "user", target_key: "work" }
        ]
      }
    });

    const { result } = renderHook(() => usePartPatchCounts());

    expect(result.current.total).toBe(3);
    expect(result.current.counts.get("user/commit")).toBe(2);
    expect(result.current.counts.get("user/work")).toBe(1);
  });

  it("пустые данные → нулевые счётчики", () => {
    mocks.usePromptPartPatchesList.mockReturnValue({ data: undefined });

    const { result } = renderHook(() => usePartPatchCounts());

    expect(result.current.total).toBe(0);
    expect(result.current.counts.size).toBe(0);
  });
});
