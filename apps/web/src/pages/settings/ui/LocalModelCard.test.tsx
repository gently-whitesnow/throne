import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import { LocalModelCard } from "./LocalModelCard";

const render = (ui: React.ReactElement) =>
  renderWithQuery(ui, { withBridge: false });

const fetchLocalModelCatalog = vi.fn<() => Promise<unknown>>();

// The hook imports its API from a relative path, so we mock that path —
// the public barrel keeps re-exporting the real selectors / meta / hook.
vi.mock("@/entities/local-model-catalog/api/local-model-catalog-api", () => ({
  fetchLocalModelCatalog: () => fetchLocalModelCatalog()
}));

describe("LocalModelCard", () => {
  beforeEach(() => {
    fetchLocalModelCatalog.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("показывает «Загружаем статус…» до первого ответа", async () => {
    let resolve!: (v: unknown) => void;
    fetchLocalModelCatalog.mockReturnValue(
      new Promise((r) => {
        resolve = r;
      })
    );

    render(<LocalModelCard />);

    expect(screen.getByText(/Загружаем статус/)).toBeTruthy();

    resolve({ status: "not_configured", models: [] });
    await waitFor(() => {
      expect(screen.queryByText(/Загружаем статус/)).toBeNull();
    });
  });

  it("рендерит список моделей и зелёный pill при status=ready", async () => {
    fetchLocalModelCatalog.mockResolvedValue({
      status: "ready",
      base_url: "http://localhost:1234",
      models: ["llama-3", "qwen-2.5"]
    });

    render(<LocalModelCard />);

    await waitFor(() => {
      expect(screen.getByText("Подключено")).toBeTruthy();
    });
    expect(screen.getByText("http://localhost:1234")).toBeTruthy();
    expect(screen.getByText("llama-3")).toBeTruthy();
    expect(screen.getByText("qwen-2.5")).toBeTruthy();
  });

  it("показывает подсказку про BaseUrl при status=not_configured", async () => {
    fetchLocalModelCatalog.mockResolvedValue({
      status: "not_configured",
      models: []
    });

    render(<LocalModelCard />);

    await waitFor(() => {
      expect(screen.getByText("Не настроено")).toBeTruthy();
    });
    expect(screen.getByText(/Задайте/)).toBeTruthy();
  });

  it("показывает ошибку probe при status=unreachable", async () => {
    fetchLocalModelCatalog.mockResolvedValue({
      status: "unreachable",
      base_url: "http://localhost:1234",
      models: [],
      error: "connection refused"
    });

    render(<LocalModelCard />);

    await waitFor(() => {
      expect(screen.getByText("Недоступен")).toBeTruthy();
    });
    expect(screen.getByText(/connection refused/)).toBeTruthy();
  });

  it("показывает «Нет моделей» при status=empty", async () => {
    fetchLocalModelCatalog.mockResolvedValue({
      status: "empty",
      base_url: "http://localhost:1234",
      models: []
    });

    render(<LocalModelCard />);

    await waitFor(() => {
      expect(screen.getByText("Нет моделей")).toBeTruthy();
    });
    expect(screen.getByText(/не объявил ни одной модели/)).toBeTruthy();
  });

  it("повторно дёргает API по клику «Проверить»", async () => {
    fetchLocalModelCatalog.mockResolvedValue({
      status: "not_configured",
      models: []
    });

    render(<LocalModelCard />);

    await waitFor(() => {
      expect(fetchLocalModelCatalog).toHaveBeenCalledTimes(1);
    });

    fireEvent.click(screen.getByRole("button", { name: /Перепроверить/ }));

    await waitFor(() => {
      expect(fetchLocalModelCatalog).toHaveBeenCalledTimes(2);
    });
  });

  it("показывает ошибку, если запрос упал", async () => {
    fetchLocalModelCatalog.mockRejectedValue(new Error("boom"));

    render(<LocalModelCard />);

    await waitFor(() => {
      expect(screen.getByRole("alert").textContent).toMatch(/boom/);
    });
  });
});
