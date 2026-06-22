import { describe, expect, it } from "vitest";

import { selectPanels, type PanelGating } from "./select-panels";

const panels: PanelGating[] = [
  { placement: "context", order: 20 },
  { placement: "primary", order: 10 },
  { placement: "review", order: 20 },
  { placement: "context", order: 10 },
  { placement: "review", order: 10 }
];

describe("selectPanels", () => {
  it("оставляет только панели запрошенного региона", () => {
    const result = selectPanels(panels, "context");
    expect(result.every((panel) => panel.placement === "context")).toBe(true);
    expect(result).toHaveLength(2);
  });

  it("сортирует панели региона по order по возрастанию", () => {
    const result = selectPanels(panels, "context");
    expect(result.map((panel) => panel.order)).toEqual([10, 20]);
  });

  it("возвращает все панели региона без гейтов", () => {
    const result = selectPanels(panels, "review");
    expect(result.map((panel) => panel.order)).toEqual([10, 20]);
  });
});
