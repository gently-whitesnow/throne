import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { RegenerateMcpTokenButton } from "./RegenerateMcpTokenButton";

const issueMcpToken = vi.fn<() => Promise<unknown>>();

vi.mock("@/entities/mcp-token", () => ({
  issueMcpToken: () => issueMcpToken()
}));

vi.mock("@/shared/api", () => ({
  HttpError: class HttpError extends Error {
    status = 500;
  }
}));

describe("RegenerateMcpTokenButton", () => {
  beforeEach(() => {
    issueMcpToken.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("не выпускает токен без подтверждения", () => {
    render(<RegenerateMcpTokenButton onIssued={() => undefined} />);

    fireEvent.click(screen.getByRole("button", { name: "Перевыпустить" }));

    expect(
      screen.getByRole("dialog", { name: "Перевыпустить MCP-токен?" })
    ).toBeTruthy();
    expect(issueMcpToken).not.toHaveBeenCalled();
  });

  it("отменяет перевыпуск и не вызывает API", () => {
    render(<RegenerateMcpTokenButton onIssued={() => undefined} />);

    fireEvent.click(screen.getByRole("button", { name: "Перевыпустить" }));
    fireEvent.click(screen.getByRole("button", { name: "Отмена" }));

    expect(screen.queryByRole("dialog")).toBeNull();
    expect(issueMcpToken).not.toHaveBeenCalled();
  });

  it("по подтверждению вызывает issueMcpToken и onIssued", async () => {
    issueMcpToken.mockResolvedValue({
      token: "new-token",
      created_at: "2026-05-05T12:00:00Z",
      last_four: "oken"
    });
    const onIssued = vi.fn();

    render(<RegenerateMcpTokenButton onIssued={onIssued} />);

    fireEvent.click(screen.getByRole("button", { name: "Перевыпустить" }));
    fireEvent.click(screen.getByRole("button", { name: "Да, перевыпустить" }));

    await waitFor(() => {
      expect(onIssued).toHaveBeenCalledWith({
        token: "new-token",
        created_at: "2026-05-05T12:00:00Z",
        last_four: "oken"
      });
    });
    expect(issueMcpToken).toHaveBeenCalledTimes(1);
  });
});
