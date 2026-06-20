import { describe, expect, it } from "vitest";

import type { PromptPartModeRole } from "@/entities/prompt-part";

import { mergeRoleForMode } from "./composition";

describe("mergeRoleForMode", () => {
  const roles: PromptPartModeRole[] = [
    { mode: "work", role: "default_on", order: 2 },
    { mode: "interview", role: "mandatory", order: 0 }
  ];

  it("none удаляет запись режима, сохраняя другие", () => {
    const next = mergeRoleForMode(roles, "work", "none", 5);
    expect(next).toEqual([{ mode: "interview", role: "mandatory", order: 0 }]);
  });

  it("смена роли сохраняет order и другие режимы", () => {
    const next = mergeRoleForMode(roles, "work", "mandatory", 99);
    expect(next).toContainEqual({
      mode: "interview",
      role: "mandatory",
      order: 0
    });
    expect(next).toContainEqual({ mode: "work", role: "mandatory", order: 2 });
    expect(next).toHaveLength(2);
  });

  it("добавляет новый режим с orderForNew", () => {
    const next = mergeRoleForMode(roles, "free", "default_off", 7);
    expect(next).toContainEqual({
      mode: "free",
      role: "default_off",
      order: 7
    });
    expect(next).toHaveLength(3);
  });
});
