import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { GitProvidersCard } from "./GitProvidersCard";

const fetchGitProvidersStatus = vi.fn<() => Promise<unknown>>();

// The hook imports its API from a relative path, so we mock that path —
// the public barrel keeps re-exporting the real selectors / meta / hook.
vi.mock("@/entities/git-provider-status/api/git-providers-status-api", () => ({
  fetchGitProvidersStatus: () => fetchGitProvidersStatus()
}));

describe("GitProvidersCard", () => {
  beforeEach(() => {
    fetchGitProvidersStatus.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("показывает «Загружаем статус…» до первого ответа", async () => {
    let resolve!: (v: unknown) => void;
    fetchGitProvidersStatus.mockReturnValue(
      new Promise((r) => {
        resolve = r;
      })
    );

    render(<GitProvidersCard />);

    expect(screen.getByText(/Загружаем статус/)).toBeTruthy();

    resolve({ github: { authenticated: false, error: "no creds" } });
    await waitFor(() => {
      expect(screen.queryByText(/Загружаем статус/)).toBeNull();
    });
  });

  it("показывает зелёный pill, login и scopes при authenticated=true", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: {
        authenticated: true,
        login: "octocat",
        scopes: ["repo", "read:org"]
      }
    });

    render(<GitProvidersCard />);

    await waitFor(() => {
      expect(screen.getByText("Подключено")).toBeTruthy();
    });
    expect(screen.getByText("octocat")).toBeTruthy();
    expect(screen.getByText("repo")).toBeTruthy();
    expect(screen.getByText("read:org")).toBeTruthy();
  });

  it("показывает красный pill и текст ошибки CLI при authenticated=false", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: {
        authenticated: false,
        error: "gh: not logged in"
      }
    });

    render(<GitProvidersCard />);

    await waitFor(() => {
      expect(screen.getByText("Нет авторизации")).toBeTruthy();
    });
    expect(screen.getByText(/gh: not logged in/)).toBeTruthy();
  });

  it("повторно дёргает API по клику «Проверить»", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: { authenticated: false, error: "no creds" }
    });

    render(<GitProvidersCard />);

    await waitFor(() => {
      expect(fetchGitProvidersStatus).toHaveBeenCalledTimes(1);
    });

    fireEvent.click(screen.getByRole("button", { name: /Перепроверить/ }));

    await waitFor(() => {
      expect(fetchGitProvidersStatus).toHaveBeenCalledTimes(2);
    });
  });

  it("показывает ошибку, если запрос упал", async () => {
    fetchGitProvidersStatus.mockRejectedValue(new Error("boom"));

    render(<GitProvidersCard />);

    await waitFor(() => {
      expect(screen.getByRole("alert").textContent).toMatch(/boom/);
    });
  });

  it("ссылка «Как настроить gh» ведёт на docs.github.com и открывается в новой вкладке", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: { authenticated: true, login: "octocat", scopes: [] }
    });

    render(<GitProvidersCard />);

    await waitFor(() => {
      expect(screen.getByText("Подключено")).toBeTruthy();
    });

    const link = screen.getByRole("link", { name: /Как настроить/ });
    expect(link.getAttribute("href")).toMatch(/^https:\/\/docs\.github\.com\//);
    expect(link.getAttribute("target")).toBe("_blank");
    expect(link.getAttribute("rel")).toContain("noopener");
  });
});
