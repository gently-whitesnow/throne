import { cleanup, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import { ReadinessPanel } from "./ReadinessPanel";

const fetchGitProvidersStatus = vi.fn<() => Promise<unknown>>();
const fetchTerminalVendorCatalog = vi.fn<() => Promise<unknown>>();
const fetchWorkspaceSettings = vi.fn<() => Promise<unknown>>();

vi.mock("@/entities/git-provider-status/api/git-providers-status-api", () => ({
  fetchGitProvidersStatus: () => fetchGitProvidersStatus(),
  setGitLabHost: vi.fn()
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

describe("ReadinessPanel", () => {
  beforeEach(() => {
    fetchGitProvidersStatus.mockReset();
    fetchTerminalVendorCatalog.mockReset();
    fetchWorkspaceSettings.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("рендерит «Throne готов» когда все критерии выполнены", async () => {
    fetchGitProvidersStatus.mockResolvedValue({
      github: { authenticated: true, state: "ok" }
    });
    fetchTerminalVendorCatalog.mockResolvedValue({
      default_vendor: "claude",
      vendors: [vendor("ready")]
    });
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
    fetchGitProvidersStatus.mockResolvedValue({
      github: { authenticated: true, state: "ok" }
    });
    fetchTerminalVendorCatalog.mockResolvedValue({
      default_vendor: "claude",
      vendors: [vendor("logged_out")]
    });
    fetchWorkspaceSettings.mockResolvedValue({ writable: true });

    render(<ReadinessPanel />);

    await waitFor(() => {
      expect(screen.getByText(/Не готов/)).toBeTruthy();
    });
    expect(screen.getAllByText("Как установить").length).toBeGreaterThan(0);
    expect(
      screen.getByTestId("readiness-item-vendor").getAttribute("data-ok")
    ).toBe("false");
  });
});
