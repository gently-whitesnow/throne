import { cleanup, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";

import { AgentVendorsCard } from "./AgentVendorsCard";

const fetchTerminalVendorCatalog = vi.fn<() => Promise<unknown>>();

vi.mock("@/entities/terminal-setting/api/terminal-vendor-catalog-api", () => ({
  fetchTerminalVendorCatalog: () => fetchTerminalVendorCatalog()
}));

const render = (ui: React.ReactElement) =>
  renderWithQuery(ui, { withBridge: false });

function vendor(
  v: string,
  label: string,
  login_status: string,
  selectable: boolean,
  login_detail: string | null = null
) {
  return {
    vendor: v,
    label,
    supports_effort: true,
    models: ["m"],
    efforts: ["medium"],
    model_source: "static",
    login_status,
    login_detail,
    selectable
  };
}

describe("AgentVendorsCard", () => {
  beforeEach(() => {
    fetchTerminalVendorCatalog.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("рендерит «Залогинен» для ready и «В разработке» для opencode", async () => {
    fetchTerminalVendorCatalog.mockResolvedValue({
      default_vendor: "claude",
      vendors: [
        vendor("claude", "Claude", "ready", true),
        vendor("opencode", "OpenCode", "in_development", false, "в разработке")
      ]
    });

    render(<AgentVendorsCard />);

    await waitFor(() => {
      expect(screen.getByTestId("agent-vendors-card")).toBeTruthy();
    });
    expect(screen.getByText("Залогинен")).toBeTruthy();
    expect(screen.getByText("В разработке")).toBeTruthy();
    expect(screen.getByText("в разработке")).toBeTruthy();
    expect(
      screen
        .getByTestId("agent-vendor-opencode")
        .getAttribute("data-login-status")
    ).toBe("in_development");
  });
});
