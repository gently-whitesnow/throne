import { describe, expect, it } from "vitest";

import type { PromptPartsComponents } from "@/shared/api";
import type {
  PromptPartListItem,
  PromptPartModeRole
} from "@/entities/prompt-part";

import {
  assembleSystemPrompt,
  buildModeComposition,
  mergeRoleForMode
} from "./composition";

type BundlesTree = PromptPartsComponents["schemas"]["BundlesTreeDto"];

function entry(
  scope: "system" | "user",
  key: string,
  text: string
): PromptPartsComponents["schemas"]["BundleEntryNodeDto"] {
  return {
    scope,
    key,
    prompt_part_id: scope === "user" ? `pid-${key}` : null,
    current_version: 1,
    text,
    editable: scope === "user",
    present: true
  };
}

const bundlesTree: BundlesTree = {
  bundles: [
    {
      mode: "work",
      includes: [
        entry("system", "system", "SYS"),
        entry("system", "common", "COMMON"),
        entry("user", "work", "WORK")
      ]
    },
    {
      mode: "interview",
      includes: [
        entry("system", "system", "SYS"),
        entry("system", "common", "COMMON"),
        entry("user", "interview", "INTERVIEW")
      ]
    }
  ]
};

function optional(
  id: string,
  key: string,
  roles: PromptPartModeRole[],
  textShort = `short-${key}`
): PromptPartListItem {
  return {
    id,
    key,
    scope: "user",
    text_short: textShort,
    current_version: 1,
    mode_roles: roles,
    created_at: "2026-01-01T00:00:00Z",
    updated_at: "2026-01-01T00:00:00Z"
  };
}

describe("buildModeComposition", () => {
  it("кладёт mandatory-инструкции перед optional, в порядке bundle", () => {
    const parts = buildModeComposition("work", bundlesTree, []);
    expect(parts.map((p) => p.key)).toEqual(["system", "common", "work"]);
    expect(parts.every((p) => p.role === "mandatory")).toBe(true);
    expect(parts.map((p) => p.order)).toEqual([0, 1, 2]);
  });

  it("free не имеет mandatory-частей", () => {
    const parts = buildModeComposition("free", bundlesTree, [
      optional("o1", "alpha", [{ mode: "free", role: "default_on", order: 0 }])
    ]);
    expect(parts.every((p) => p.kind === "optional")).toBe(true);
    expect(parts).toHaveLength(1);
  });

  it("сортирует optional по (order, key) и ставит их после mandatory", () => {
    const parts = buildModeComposition("work", bundlesTree, [
      optional("o2", "zeta", [{ mode: "work", role: "default_on", order: 1 }]),
      optional("o1", "beta", [{ mode: "work", role: "default_off", order: 1 }]),
      optional("o3", "alpha", [{ mode: "work", role: "mandatory", order: 0 }]),
      optional("oX", "skip", [
        { mode: "interview", role: "default_on", order: 0 }
      ])
    ]);
    const optionalKeys = parts
      .filter((p) => p.kind === "optional")
      .map((p) => p.key);
    expect(optionalKeys).toEqual(["alpha", "beta", "zeta"]);
    // mandatory block first
    expect(parts[0].kind).toBe("mandatory");
  });

  it("selected = mandatory + default_on, не default_off", () => {
    const parts = buildModeComposition("work", bundlesTree, [
      optional("o1", "a", [{ mode: "work", role: "mandatory", order: 0 }]),
      optional("o2", "b", [{ mode: "work", role: "default_on", order: 1 }]),
      optional("o3", "c", [{ mode: "work", role: "default_off", order: 2 }])
    ]);
    const byKey = Object.fromEntries(parts.map((p) => [p.key, p.selected]));
    expect(byKey.a).toBe(true);
    expect(byKey.b).toBe(true);
    expect(byKey.c).toBe(false);
  });

  it("использует полный текст из optionalTexts когда доступен", () => {
    const parts = buildModeComposition(
      "work",
      bundlesTree,
      [optional("o1", "a", [{ mode: "work", role: "default_on", order: 0 }])],
      { o1: "FULL TEXT" }
    );
    const opt = parts.find((p) => p.kind === "optional");
    expect(opt?.text).toBe("FULL TEXT");
    expect(opt?.textIsFull).toBe(true);
  });
});

describe("assembleSystemPrompt", () => {
  it("берёт только selected, тримит, выкидывает пустые, join \\n\\n", () => {
    const parts = buildModeComposition("work", bundlesTree, [
      optional(
        "o1",
        "a",
        [{ mode: "work", role: "default_on", order: 0 }],
        "  OPT  "
      ),
      optional(
        "o2",
        "b",
        [{ mode: "work", role: "default_off", order: 1 }],
        "HIDDEN"
      )
    ]);
    expect(assembleSystemPrompt(parts)).toBe("SYS\n\nCOMMON\n\nWORK\n\nOPT");
  });

  it("отбрасывает части с пустым текстом", () => {
    const parts = buildModeComposition("free", undefined, [
      optional(
        "o1",
        "a",
        [{ mode: "free", role: "default_on", order: 0 }],
        "   "
      ),
      optional("o2", "b", [{ mode: "free", role: "default_on", order: 1 }], "X")
    ]);
    expect(assembleSystemPrompt(parts)).toBe("X");
  });
});

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
