import { describe, expect, it } from "vitest";

import { selectPanels, type PanelGating } from "./select-panels";

const allEnabled = () => true;
const noneEnabled = () => false;

const panels: PanelGating[] = [
  { placement: "context", order: 20 },
  { placement: "primary", order: 10 },
  { placement: "review", order: 20, capability: "repositories" },
  { placement: "context", order: 10 },
  { placement: "review", order: 10, capability: "repositories" }
];

describe("selectPanels", () => {
  it("оставляет только панели запрошенного региона", () => {
    const result = selectPanels(panels, "context", allEnabled);
    expect(result.every((panel) => panel.placement === "context")).toBe(true);
    expect(result).toHaveLength(2);
  });

  it("сортирует панели региона по order по возрастанию", () => {
    const result = selectPanels(panels, "context", allEnabled);
    expect(result.map((panel) => panel.order)).toEqual([10, 20]);
  });

  it("скрывает панели с выключенным capability", () => {
    const result = selectPanels(panels, "review", noneEnabled);
    expect(result).toHaveLength(0);
  });

  it("показывает панели с включённым capability", () => {
    const result = selectPanels(panels, "review", allEnabled);
    expect(result.map((panel) => panel.order)).toEqual([10, 20]);
  });

  it("гейт спрашивается именно по объявленному capability", () => {
    const asked: string[] = [];
    selectPanels(panels, "review", (capability) => {
      asked.push(capability);
      return true;
    });
    expect(asked).toEqual(["repositories", "repositories"]);
  });
});
