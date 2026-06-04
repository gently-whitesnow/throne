import { renderHook } from "@testing-library/react";
import { createRef } from "react";
import { describe, expect, it } from "vitest";

import { useCanvasViewport, type WorldBounds } from "./use-canvas-viewport";

// jsdom не считает layout — clientWidth/Height всегда 0, и fitToView ушёл бы в
// ранний return. Подкладываем стейдж фиксированного размера.
function stageRef(w: number, h: number) {
  const el = document.createElement("div");
  Object.defineProperty(el, "clientWidth", { value: w, configurable: true });
  Object.defineProperty(el, "clientHeight", { value: h, configurable: true });
  const ref = createRef<HTMLDivElement>();
  (ref as { current: HTMLDivElement }).current = el;
  return ref;
}

describe("useCanvasViewport — авто-fit", () => {
  it("делает fit один раз, когда мир получает ненулевые размеры", () => {
    const ref = stageRef(800, 600);
    const { result } = renderHook(
      ({ worldBounds }: { worldBounds: WorldBounds | null }) =>
        useCanvasViewport({ stageRef: ref, worldBounds, fitKey: "ctx" }),
      { initialProps: { worldBounds: { w: 1000, h: 400 } } }
    );

    // (800-128)/1000=0.672 < (600-128)/400 → scale=0.672
    expect(result.current.viewport.scale).toBeCloseTo(0.672, 3);
  });

  it("не сбрасывает viewport на смену bounds в пределах того же контекста", () => {
    const ref = stageRef(800, 600);
    const { result, rerender } = renderHook(
      ({ worldBounds }: { worldBounds: WorldBounds | null }) =>
        useCanvasViewport({ stageRef: ref, worldBounds, fitKey: "ctx" }),
      { initialProps: { worldBounds: { w: 1000, h: 400 } } }
    );
    const fitted = result.current.viewport.scale;

    // Интент → done: bounds меняются, но fitKey тот же — re-fit не должен
    // случиться, иначе пользователю «сбрасывает роадмап».
    rerender({ worldBounds: { w: 1200, h: 500 } });

    expect(result.current.viewport.scale).toBeCloseTo(fitted, 5);
  });

  it("делает re-fit при смене контекста (fitKey)", () => {
    const ref = stageRef(800, 600);
    const { result, rerender } = renderHook(
      ({
        worldBounds,
        fitKey
      }: {
        worldBounds: WorldBounds | null;
        fitKey: string;
      }) => useCanvasViewport({ stageRef: ref, worldBounds, fitKey }),
      { initialProps: { worldBounds: { w: 1000, h: 400 }, fitKey: "a" } }
    );
    const first = result.current.viewport.scale;

    rerender({ worldBounds: { w: 1200, h: 500 }, fitKey: "b" });

    // (800-128)/1200=0.56 — новый граф пересобрал viewport.
    expect(result.current.viewport.scale).not.toBeCloseTo(first, 3);
    expect(result.current.viewport.scale).toBeCloseTo(0.56, 3);
  });
});
