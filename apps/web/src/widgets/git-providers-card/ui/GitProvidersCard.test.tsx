import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import { GitProvidersCard } from "./GitProvidersCard";

const render = (ui: React.ReactElement) =>
  renderWithQuery(ui, { withBridge: false });

const fetchGitProvidersStatus = vi.fn<() => Promise<unknown>>();
const setGitLabHost = vi.fn<(host: string) => Promise<{ host: string }>>();

// The hook imports its API from a relative path, so we mock that path —
// the public barrel keeps re-exporting the real selectors / meta / hook.
vi.mock("@/entities/git-provider-status/api/git-providers-status-api", () => ({
  fetchGitProvidersStatus: () => fetchGitProvidersStatus(),
  setGitLabHost: (host: string) => setGitLabHost(host)
}));

describe("GitProvidersCard", () => {
  beforeEach(() => {
    fetchGitProvidersStatus.mockReset();
    setGitLabHost.mockReset();
    setGitLabHost.mockImplementation((host: string) =>
      Promise.resolve({ host })
    );
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

    resolve({
      github: {
        authenticated: false,
        state: "unauthenticated",
        error: "no creds"
      },
      gitlab: { authenticated: false, state: "missing", error: "no host" }
    });
    await waitFor(() => {
      expect(screen.queryByText(/Загружаем статус/)).toBeNull();
    });
  });

  it("показывает зелёный pill, login и scopes при authenticated=true", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: {
        authenticated: true,
        state: "authenticated",
        login: "octocat",
        scopes: ["repo", "read:org"]
      },
      gitlab: {
        authenticated: false,
        state: "missing",
        error: "Throne:GitLab:Host is not configured."
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
        state: "unauthenticated",
        error: "gh: not logged in"
      },
      gitlab: {
        authenticated: false,
        state: "missing",
        error: "no host"
      }
    });

    render(<GitProvidersCard />);

    await waitFor(() => {
      expect(screen.getByText("Нет авторизации")).toBeTruthy();
    });
    expect(screen.getByText(/gh: not logged in/)).toBeTruthy();
  });

  it("показывает янтарный GitLab статус при offline", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: { authenticated: true, state: "authenticated", login: "octocat" },
      gitlab: {
        authenticated: false,
        state: "offline",
        host: "gitlab.example.com",
        error: "timeout"
      }
    });

    render(<GitProvidersCard />);

    await waitFor(() => {
      expect(screen.getByText("Вне сети")).toBeTruthy();
    });
    expect(screen.getByText(/gitlab.example.com/)).toBeTruthy();
    expect(screen.getByText(/timeout/)).toBeTruthy();
  });

  it("повторно дёргает API по клику «Проверить»", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: {
        authenticated: false,
        state: "unauthenticated",
        error: "no creds"
      },
      gitlab: { authenticated: false, state: "missing", error: "no host" }
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

  it("сохраняет новый GitLab host по кнопке «Сохранить»", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: { authenticated: true, state: "authenticated", login: "octocat" },
      gitlab: {
        authenticated: true,
        state: "authenticated",
        host: "gitlab.com",
        login: "me"
      }
    });

    render(<GitProvidersCard />);

    const input =
      await screen.findByTestId<HTMLInputElement>("gitlab-host-input");
    expect(input.value).toBe("gitlab.com");

    fireEvent.change(input, { target: { value: "gitlab.example.com" } });
    fireEvent.click(screen.getByTestId("gitlab-host-save"));

    await waitFor(() => {
      expect(setGitLabHost).toHaveBeenCalledWith("gitlab.example.com");
    });
  });

  it("inline-ошибка валидации при пустом host'е и mutation не дёргается", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: { authenticated: true, state: "authenticated", login: "octocat" },
      gitlab: {
        authenticated: true,
        state: "authenticated",
        host: "gitlab.com"
      }
    });

    render(<GitProvidersCard />);

    const input =
      await screen.findByTestId<HTMLInputElement>("gitlab-host-input");
    fireEvent.change(input, { target: { value: "   " } });
    fireEvent.click(screen.getByTestId("gitlab-host-save"));

    expect(screen.getByTestId("gitlab-host-error").textContent).toMatch(
      /Введите имя хоста/
    );
    expect(setGitLabHost).not.toHaveBeenCalled();
  });

  it("inline-ошибка валидации при host'е со схемой", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: { authenticated: true, state: "authenticated", login: "octocat" },
      gitlab: {
        authenticated: true,
        state: "authenticated",
        host: "gitlab.com"
      }
    });

    render(<GitProvidersCard />);

    const input =
      await screen.findByTestId<HTMLInputElement>("gitlab-host-input");
    fireEvent.change(input, {
      target: { value: "https://gitlab.example.com" }
    });
    fireEvent.click(screen.getByTestId("gitlab-host-save"));

    expect(screen.getByTestId("gitlab-host-error").textContent).toMatch(
      /без схемы/
    );
    expect(setGitLabHost).not.toHaveBeenCalled();
  });

  it("ссылка «Как настроить gh» ведёт на docs.github.com и открывается в новой вкладке", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: {
        authenticated: true,
        state: "authenticated",
        login: "octocat",
        scopes: []
      },
      gitlab: { authenticated: false, state: "missing", error: "no host" }
    });

    render(<GitProvidersCard />);

    await waitFor(() => {
      expect(screen.getByText("Подключено")).toBeTruthy();
    });

    const link = screen.getByRole("link", { name: /Как настроить gh/ });
    expect(link.getAttribute("href")).toMatch(/^https:\/\/docs\.github\.com\//);
    expect(link.getAttribute("target")).toBe("_blank");
    expect(link.getAttribute("rel")).toContain("noopener");
  });
});
