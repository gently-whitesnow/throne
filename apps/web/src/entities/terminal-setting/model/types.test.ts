import { describe, expect, it } from "vitest";

import {
  findVendorMetadata,
  resolveDefaultVendor,
  type TerminalVendorCatalog
} from "./types";

const catalog: TerminalVendorCatalog = {
  default_vendor: "claude",
  vendors: [
    {
      vendor: "claude",
      label: "Claude",
      supports_effort: true,
      models: ["opus", "sonnet", "haiku"],
      default_model: "opus",
      efforts: ["low", "medium", "high", "xhigh"],
      default_effort: "high",
      model_source: "static"
    },
    {
      vendor: "codex",
      label: "Codex",
      supports_effort: true,
      models: ["gpt-5.5", "gpt-5.4"],
      default_model: "gpt-5.5",
      efforts: ["low", "medium", "high", "xhigh"],
      default_effort: "medium",
      model_source: "static"
    }
  ]
};

describe("findVendorMetadata", () => {
  it("возвращает metadata зарегистрированного вендора", () => {
    expect(findVendorMetadata(catalog, "codex")?.default_model).toBe("gpt-5.5");
  });

  it("undefined для незагруженного каталога", () => {
    expect(findVendorMetadata(undefined, "claude")).toBeUndefined();
  });
});

describe("resolveDefaultVendor", () => {
  it("undefined, пока каталог не загружен", () => {
    expect(resolveDefaultVendor(undefined, "codex")).toBeUndefined();
  });

  it("persisted-настройка главнее дефолта каталога", () => {
    expect(resolveDefaultVendor(catalog, "codex")).toBe("codex");
  });

  it("дефолт каталога, когда настройка не задана", () => {
    expect(resolveDefaultVendor(catalog, undefined)).toBe("claude");
  });

  it("дефолт каталога, когда настройка указывает на неизвестный вендор", () => {
    expect(resolveDefaultVendor(catalog, "opencode" as never)).toBe("claude");
  });
});
