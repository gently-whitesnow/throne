import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import { McpTokenCard } from "./McpTokenCard";

const render = (ui: React.ReactElement) =>
  renderWithQuery(ui, { withBridge: false });

const fetchMcpTokenMeta = vi.fn<() => Promise<unknown>>();
const issueMcpToken = vi.fn<() => Promise<unknown>>();

vi.mock("@/entities/mcp-token/api/mcp-tokens-api", () => ({
  fetchMcpTokenMeta: () => fetchMcpTokenMeta(),
  issueMcpToken: () => issueMcpToken()
}));

describe("McpTokenCard", () => {
  beforeEach(() => {
    fetchMcpTokenMeta.mockReset();
    issueMcpToken.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("показывает приглашение сгенерировать, если токена нет", async () => {
    fetchMcpTokenMeta.mockResolvedValue({ has_token: false });

    render(<McpTokenCard />);

    await waitFor(() => {
      expect(screen.getByText(/Активного токена пока нет/)).toBeTruthy();
    });
    expect(
      screen.getByRole("button", { name: /Сгенерировать токен/ })
    ).toBeTruthy();
  });

  it("показывает мета и кнопку перевыпуска, если токен уже есть", async () => {
    fetchMcpTokenMeta.mockResolvedValue({
      has_token: true,
      created_at: "2026-05-01T10:00:00Z",
      last_four: "Ab12"
    });

    render(<McpTokenCard />);

    await waitFor(() => {
      expect(screen.getByText("••••Ab12")).toBeTruthy();
    });
    expect(screen.getByRole("button", { name: "Перевыпустить" })).toBeTruthy();
  });

  it("показывает выпущенный секрет один раз и убирает его по запросу", async () => {
    fetchMcpTokenMeta.mockResolvedValue({ has_token: false });
    issueMcpToken.mockResolvedValue({
      token: "secret-plaintext-xyz",
      created_at: "2026-05-05T12:00:00Z",
      last_four: "txyz"
    });

    render(<McpTokenCard />);

    fireEvent.click(
      await screen.findByRole("button", { name: /Сгенерировать токен/ })
    );

    await waitFor(() => {
      expect(screen.getByText("secret-plaintext-xyz")).toBeTruthy();
    });
    expect(screen.getByText(/повторно показан не будет/)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Я сохранил, скрыть" }));

    expect(screen.queryByText("secret-plaintext-xyz")).toBeNull();
    expect(screen.getByText("••••txyz")).toBeTruthy();
  });

  it("очищает секрет при размонтировании страницы", async () => {
    fetchMcpTokenMeta.mockResolvedValue({ has_token: false });
    issueMcpToken.mockResolvedValue({
      token: "ephemeral-secret",
      created_at: "2026-05-05T12:00:00Z",
      last_four: "cret"
    });

    const { unmount } = render(<McpTokenCard />);

    fireEvent.click(
      await screen.findByRole("button", { name: /Сгенерировать токен/ })
    );

    await waitFor(() => {
      expect(screen.getByText("ephemeral-secret")).toBeTruthy();
    });

    unmount();

    expect(document.body.textContent).not.toContain("ephemeral-secret");
  });
});
