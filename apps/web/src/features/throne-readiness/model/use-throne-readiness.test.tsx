import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createTestQueryClient } from "@/app/test-utils";

import { useThroneReadiness } from "./use-throne-readiness";

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

function vendor(login_status: string, selectable = true) {
  return {
    vendor: "claude",
    label: "Claude",
    supports_effort: true,
    models: ["sonnet"],
    efforts: ["medium"],
    model_source: "static",
    login_status,
    login_detail: null,
    selectable
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

function wrapper({ children }: { children: ReactNode }) {
  const client = createTestQueryClient();
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe("useThroneReadiness", () => {
  beforeEach(() => {
    fetchGitProvidersStatus.mockReset();
    fetchTerminalVendorCatalog.mockReset();
    fetchWorkspaceSettings.mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("ready=true когда все критерии выполнены", async () => {
    fetchGitProvidersStatus.mockResolvedValue(gitStatus(true));
    fetchTerminalVendorCatalog.mockResolvedValue({
      default_vendor: "claude",
      vendors: [vendor("ready")]
    });
    fetchWorkspaceSettings.mockResolvedValue({ writable: true });

    const { result } = renderHook(() => useThroneReadiness(), { wrapper });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });
    expect(result.current.ready).toBe(true);
    expect(result.current.items.every((i) => i.ok)).toBe(true);
  });

  it("ready=false когда вендор не залогинен", async () => {
    fetchGitProvidersStatus.mockResolvedValue(gitStatus(true));
    fetchTerminalVendorCatalog.mockResolvedValue({
      default_vendor: "claude",
      vendors: [vendor("in_development", false)]
    });
    fetchWorkspaceSettings.mockResolvedValue({ writable: true });

    const { result } = renderHook(() => useThroneReadiness(), { wrapper });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });
    expect(result.current.ready).toBe(false);
    const vendorItem = result.current.items.find((i) => i.key === "vendor");
    expect(vendorItem?.ok).toBe(false);
  });
});
