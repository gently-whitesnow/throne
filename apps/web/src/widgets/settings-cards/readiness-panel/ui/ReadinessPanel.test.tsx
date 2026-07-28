import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import { ReadinessPanel } from "./ReadinessPanel";

const fetchGitProvidersStatus = vi.fn<() => Promise<unknown>>();
const setGitLabHost = vi.fn<(host: string) => Promise<{ host: string }>>();
const fetchTerminalVendorCatalog = vi.fn<() => Promise<unknown>>();
const fetchWorkspaceSettings = vi.fn<() => Promise<unknown>>();

vi.mock("@/entities/git-provider-status/api/git-providers-status-api", () => ({
  fetchGitProvidersStatus: () => fetchGitProvidersStatus(),
  setGitLabHost: (host: string) => setGitLabHost(host)
}));
vi.mock("@/entities/terminal-setting/api/terminal-vendor-catalog-api", () => ({
  fetchTerminalVendorCatalog: () => fetchTerminalVendorCatalog()
}));
vi.mock("@/entities/workspace-setting/api/workspace-settings-api", () => ({
  fetchWorkspaceSettings: () => fetchWorkspaceSettings(),
  cleanWorkspace: vi.fn()
}));

const render = (ui: React.ReactElement) =>
  renderWithQuery(ui, { withBridge: false });

function vendor(login_status: string) {
  return {
    vendor: "claude",
    label: "Claude",
    supports_effort: true,
    models: ["sonnet"],
    efforts: ["medium"],
    model_source: "static",
    login_status,
    login_detail: null,
    selectable: true
  };
}

function catalog(login_status: string, tmuxDetected: boolean) {
  return {
    default_vendor: "claude",
    vendors: [vendor(login_status)],
    runtime: { tmux: { detected: tmuxDetected, detail: null } }
  };
}

function gitStatus(authenticated: boolean) {
  return {
    providers: [
      {
        provider: "github",
        status: {
          authenticated,
          state: authenticated ? "authenticated" : "missing"
        }
      }
    ]
  };
}

function bothGitProviders(
  gitHubAuthenticated: boolean,
  gitLabAuthenticated: boolean,
  gitLabHost: string
) {
  return {
    providers: [
      {
        provider: "github",
        status: {
          authenticated: gitHubAuthenticated,
          state: gitHubAuthenticated ? "authenticated" : "unauthenticated"
        }
      },
      {
        provider: "gitlab",
        status: {
          authenticated: gitLabAuthenticated,
          state: gitLabAuthenticated ? "authenticated" : "unauthenticated",
          host: gitLabHost
        }
      }
    ]
  };
}

describe("ReadinessPanel", () => {
  beforeEach(() => {
    fetchGitProvidersStatus.mockReset();
    setGitLabHost.mockReset();
    setGitLabHost.mockImplementation((host) => Promise.resolve({ host }));
    fetchTerminalVendorCatalog.mockReset();
    fetchWorkspaceSettings.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("рендерит «Throne готов» когда все критерии выполнены", async () => {
    fetchGitProvidersStatus.mockResolvedValue(gitStatus(true));
    fetchTerminalVendorCatalog.mockResolvedValue(catalog("ready", true));
    fetchWorkspaceSettings.mockResolvedValue({ writable: true });

    render(<ReadinessPanel />);

    await waitFor(() => {
      expect(screen.getByText("Throne готов")).toBeTruthy();
    });
    expect(
      screen.getByTestId("readiness-panel").getAttribute("data-ready")
    ).toBe("true");
  });

  it("рендерит «Не готов» при незакрытом критерии вендора", async () => {
    fetchGitProvidersStatus.mockResolvedValue(gitStatus(true));
    fetchTerminalVendorCatalog.mockResolvedValue(catalog("logged_out", true));
    fetchWorkspaceSettings.mockResolvedValue({ writable: true });

    render(<ReadinessPanel />);

    await waitFor(() => {
      expect(screen.getByText(/Не готов/)).toBeTruthy();
    });
    // Паритет агентов: Claude и Codex — вкладками фикса.
    expect(screen.getByRole("tab", { name: "Claude" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Codex" })).toBeTruthy();
    expect(
      screen.getByTestId("readiness-item-vendor").getAttribute("data-ok")
    ).toBe("false");
  });

  it("git-пункт даёт паритетные вкладки GitHub/GitLab", async () => {
    fetchGitProvidersStatus.mockResolvedValue(gitStatus(false));
    fetchTerminalVendorCatalog.mockResolvedValue(catalog("ready", true));
    fetchWorkspaceSettings.mockResolvedValue({ writable: true });

    render(<ReadinessPanel />);

    await waitFor(() => {
      expect(screen.getByText(/Не готов/)).toBeTruthy();
    });
    expect(screen.getByRole("tab", { name: "GitHub" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "GitLab" })).toBeTruthy();
    expect(screen.getByText("gh auth login")).toBeTruthy();
  });

  it("сохраняет GitLab host в онбординге и перепроверяет сохранённый host", async () => {
    fetchGitProvidersStatus
      .mockResolvedValueOnce(bothGitProviders(false, false, "gitlab.com"))
      .mockResolvedValueOnce(bothGitProviders(false, false, "gitlab.ati.st"))
      .mockResolvedValueOnce(bothGitProviders(false, true, "gitlab.ati.st"));
    fetchTerminalVendorCatalog.mockResolvedValue(catalog("ready", true));
    fetchWorkspaceSettings.mockResolvedValue({ writable: true });

    render(<ReadinessPanel />);

    await screen.findByRole("tab", { name: "GitLab" });
    fireEvent.click(screen.getByRole("tab", { name: "GitLab" }));

    const input =
      await screen.findByTestId<HTMLInputElement>("gitlab-host-input");
    expect(input.value).toBe("gitlab.com");
    expect(
      screen.getByText("glab auth login --hostname gitlab.com")
    ).toBeTruthy();

    fireEvent.change(input, { target: { value: "gitlab.ati.st" } });
    fireEvent.click(screen.getByTestId("gitlab-host-save"));

    await waitFor(() => {
      expect(setGitLabHost).toHaveBeenCalledWith("gitlab.ati.st");
      expect(
        screen.getByText("glab auth login --hostname gitlab.ati.st")
      ).toBeTruthy();
    });

    fireEvent.click(screen.getByRole("button", { name: "Перепроверить" }));

    await waitFor(() => {
      expect(
        screen.getByTestId("readiness-item-git").getAttribute("data-ok")
      ).toBe("true");
    });
  });

  it("не отправляет невалидный GitLab host из онбординга", async () => {
    fetchGitProvidersStatus.mockResolvedValue(
      bothGitProviders(false, false, "gitlab.com")
    );
    fetchTerminalVendorCatalog.mockResolvedValue(catalog("ready", true));
    fetchWorkspaceSettings.mockResolvedValue({ writable: true });

    render(<ReadinessPanel />);

    await screen.findByRole("tab", { name: "GitLab" });
    fireEvent.click(screen.getByRole("tab", { name: "GitLab" }));
    fireEvent.change(screen.getByTestId("gitlab-host-input"), {
      target: { value: "https://gitlab.ati.st" }
    });
    fireEvent.click(screen.getByTestId("gitlab-host-save"));

    expect(screen.getByTestId("gitlab-host-error").textContent).toMatch(
      /без схемы/
    );
    expect(setGitLabHost).not.toHaveBeenCalled();
  });
});
