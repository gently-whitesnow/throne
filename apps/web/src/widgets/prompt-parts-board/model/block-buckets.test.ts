import { describe, expect, it } from "vitest";

import type {
  PromptPartListItem,
  PromptPartModeRole
} from "@/entities/prompt-part";

import {
  bucketForRole,
  bucketize,
  includedCount,
  roleForMode
} from "./block-buckets";

function part(id: string, roles: PromptPartModeRole[]): PromptPartListItem {
  return {
    id,
    key: id,
    scope: "user",
    text_short: "",
    current_version: 1,
    mode_roles: roles,
    created_at: "2026-01-01T00:00:00Z",
    updated_at: "2026-01-01T00:00:00Z"
  };
}

describe("block-buckets", () => {
  it("bucketForRole: mandatory/default_on входят, default_off доступен, none — нет", () => {
    expect(bucketForRole("mandatory")).toBe("included");
    expect(bucketForRole("default_on")).toBe("included");
    expect(bucketForRole("default_off")).toBe("available");
    expect(bucketForRole("none")).toBe("excluded");
  });

  it("roleForMode читает роль режима, по умолчанию none", () => {
    expect(roleForMode(part("a", []), "work")).toBe("none");
    expect(
      roleForMode(
        part("a", [{ mode: "work", role: "mandatory", order: 0 }]),
        "work"
      )
    ).toBe("mandatory");
  });

  it("bucketize раскладывает блоки по выбранному режиму", () => {
    const parts = [
      part("m", [{ mode: "work", role: "mandatory", order: 0 }]),
      part("on", [{ mode: "work", role: "default_on", order: 1 }]),
      part("off", [{ mode: "work", role: "default_off", order: 2 }]),
      part("ex", [{ mode: "review", role: "mandatory", order: 0 }])
    ];

    const b = bucketize(parts, "work");
    expect(b.included.map((p) => p.id)).toEqual(["m", "on"]);
    expect(b.available.map((p) => p.id)).toEqual(["off"]);
    expect(b.excluded.map((p) => p.id)).toEqual(["ex"]);

    expect(includedCount(parts, "work")).toBe(2);
    expect(includedCount(parts, "review")).toBe(1);
    expect(includedCount(parts, "free")).toBe(0);
  });
});
