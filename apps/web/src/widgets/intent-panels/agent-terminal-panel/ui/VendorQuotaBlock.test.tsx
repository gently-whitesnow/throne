import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { TerminalVendorMetadata } from "@/entities/terminal-setting";

import { VendorQuotaBlock } from "./VendorQuotaBlock";

function vendor(
  overrides: Partial<TerminalVendorMetadata> = {}
): TerminalVendorMetadata {
  return {
    vendor: "claude",
    label: "Claude",
    supports_effort: true,
    models: ["opus"],
    default_model: "opus",
    efforts: ["high"],
    default_effort: "high",
    model_source: "static",
    login_status: "ready",
    selectable: true,
    ...overrides
  };
}

afterEach(() => {
  cleanup();
});

describe("VendorQuotaBlock", () => {
  it("рендерит 5h / weekly / credits, когда quota непустая", () => {
    render(
      <VendorQuotaBlock
        vendor={vendor({
          quota: {
            five_hour: {
              used_percent: 42.6,
              resets_at: "2026-07-10T15:00:00Z"
            },
            seven_day: { used_percent: 12.0, resets_at: null },
            credits_balance: 3.14
          }
        })}
        isRefreshing={false}
        onRefresh={vi.fn()}
      />
    );

    expect(screen.getByTestId("agent-terminal-quota-5ч").textContent).toContain(
      "43%"
    );
    expect(
      screen.getByTestId("agent-terminal-quota-неделя").textContent
    ).toContain("12%");
    expect(
      screen.getByTestId("agent-terminal-quota-credits").textContent
    ).toContain("3.14");
  });

  it("скрывает блок значений, когда quota=null (только кнопка обновления)", () => {
    render(
      <VendorQuotaBlock
        vendor={vendor({ quota: undefined })}
        isRefreshing={false}
        onRefresh={vi.fn()}
      />
    );

    expect(screen.queryByTestId("agent-terminal-quota-5ч")).toBeNull();
    expect(screen.getByTestId("agent-terminal-quota-refresh")).not.toBeNull();
  });

  it("клик по «Обновить» дёргает onRefresh; во время refetch — disabled", () => {
    const onRefresh = vi.fn();
    const { rerender } = render(
      <VendorQuotaBlock
        vendor={vendor()}
        isRefreshing={false}
        onRefresh={onRefresh}
      />
    );

    fireEvent.click(screen.getByTestId("agent-terminal-quota-refresh"));
    expect(onRefresh).toHaveBeenCalledOnce();

    rerender(
      <VendorQuotaBlock
        vendor={vendor()}
        isRefreshing={true}
        onRefresh={onRefresh}
      />
    );
    const btn = screen.getByTestId("agent-terminal-quota-refresh");
    expect(btn.hasAttribute("disabled")).toBe(true);
  });
});
