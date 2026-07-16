import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { PullRequestMergeStatus } from "@/entities/review-workspace";

import { ReviewMergeControl } from "./ReviewMergeControl";

afterEach(cleanup);

function mergeable(): PullRequestMergeStatus {
  return {
    mergeability: "mergeable",
    checks: "passing",
    html_url: "https://github.com/o/r/pull/42"
  };
}

function openSettings() {
  fireEvent.click(screen.getByRole("button", { name: "Настройки мержа" }));
}

describe("ReviewMergeControl", () => {
  it("мержит выбранной стратегией; удаление ветки включено по умолчанию", () => {
    const onMerge = vi.fn();
    render(
      <ReviewMergeControl
        kind="PR"
        status={mergeable()}
        statusLoading={false}
        merging={false}
        mergeError={null}
        cleanup={true}
        onCleanupChange={vi.fn()}
        onMerge={onMerge}
      />
    );

    openSettings();

    const deleteBranch =
      screen.getByLabelText<HTMLInputElement>("Удалить ветку");
    expect(deleteBranch.checked).toBe(true);

    fireEvent.change(screen.getByLabelText("Стратегия мержа"), {
      target: { value: "squash" }
    });
    fireEvent.click(screen.getByRole("button", { name: /Смержить/ }));

    expect(onMerge).toHaveBeenCalledWith("squash", true);
  });

  it("снятие «Удалить ветку» передаёт false", () => {
    const onMerge = vi.fn();
    render(
      <ReviewMergeControl
        kind="PR"
        status={mergeable()}
        statusLoading={false}
        merging={false}
        mergeError={null}
        cleanup={true}
        onCleanupChange={vi.fn()}
        onMerge={onMerge}
      />
    );

    openSettings();
    fireEvent.click(screen.getByLabelText("Удалить ветку"));
    fireEvent.click(screen.getByRole("button", { name: /Смержить/ }));

    expect(onMerge).toHaveBeenCalledWith("merge", false);
  });

  it("«Очистить состояние» отражает проп и сообщает изменение наверх", () => {
    const onCleanupChange = vi.fn();
    render(
      <ReviewMergeControl
        kind="PR"
        status={mergeable()}
        statusLoading={false}
        merging={false}
        mergeError={null}
        cleanup={true}
        onCleanupChange={onCleanupChange}
        onMerge={vi.fn()}
      />
    );

    openSettings();
    const cleanupBox =
      screen.getByLabelText<HTMLInputElement>("Очистить состояние");
    expect(cleanupBox.checked).toBe(true);

    fireEvent.click(cleanupBox);

    expect(onCleanupChange).toHaveBeenCalledWith(false);
  });

  it("блокирует мерж и показывает ссылку на провайдера, когда мерж недоступен", () => {
    const onMerge = vi.fn();
    render(
      <ReviewMergeControl
        kind="PR"
        status={{
          mergeability: "conflicting",
          checks: "failing",
          html_url: "https://github.com/o/r/pull/42"
        }}
        statusLoading={false}
        merging={false}
        mergeError={null}
        cleanup={true}
        onCleanupChange={vi.fn()}
        onMerge={onMerge}
      />
    );

    expect(
      screen.getByRole("button", { name: /Смержить/ }).hasAttribute("disabled")
    ).toBe(true);
    const link = screen.getByRole("link", { name: /Открыть PR/ });
    expect(link.getAttribute("href")).toBe("https://github.com/o/r/pull/42");
    expect(screen.getByText("Конфликты")).toBeTruthy();
  });

  it("показывает причину отказа мержа", () => {
    render(
      <ReviewMergeControl
        kind="MR"
        status={mergeable()}
        statusLoading={false}
        merging={false}
        mergeError="Provider refused: branch protection"
        cleanup={true}
        onCleanupChange={vi.fn()}
        onMerge={vi.fn()}
      />
    );

    expect(screen.getByRole("alert").textContent).toContain(
      "branch protection"
    );
  });

  it("настройки скрыты по умолчанию и открываются кнопкой меню", () => {
    render(
      <ReviewMergeControl
        kind="PR"
        status={mergeable()}
        statusLoading={false}
        merging={false}
        mergeError={null}
        cleanup={true}
        onCleanupChange={vi.fn()}
        onMerge={vi.fn()}
      />
    );

    expect(screen.queryByLabelText("Удалить ветку")).toBeNull();
    expect(screen.queryByLabelText("Стратегия мержа")).toBeNull();

    openSettings();

    expect(screen.getByLabelText("Удалить ветку")).toBeTruthy();
    expect(screen.getByLabelText("Стратегия мержа")).toBeTruthy();
  });
});
