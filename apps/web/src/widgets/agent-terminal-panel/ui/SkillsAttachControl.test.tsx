import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { AvailableSessionSkill } from "../model/types";

import { SkillsAttachControl } from "./SkillsAttachControl";

function skill(
  id: string,
  overrides: Partial<AvailableSessionSkill> = {}
): AvailableSessionSkill {
  return {
    skill_id: id,
    source: "throne",
    title: `Skill ${id}`,
    description: `desc ${id}`,
    materializable: true,
    reason: null,
    default_enabled: false,
    selected: false,
    ...overrides
  };
}

afterEach(() => {
  cleanup();
});

describe("SkillsAttachControl", () => {
  it("рендерит бейдж по каждому загруженному (selected) скилу", () => {
    render(
      <SkillsAttachControl
        available={[
          skill("alpha", { title: "Alpha", selected: true }),
          skill("beta", { title: "Beta", selected: true }),
          skill("gamma", { title: "Gamma", selected: false })
        ]}
        sessionLive
        isLoadingAvailable={false}
        isAttaching={false}
        onAttach={vi.fn()}
      />
    );

    expect(
      screen.getByTestId("agent-terminal-skill-badge-alpha").textContent
    ).toBe("Alpha");
    expect(
      screen.getByTestId("agent-terminal-skill-badge-beta").textContent
    ).toBe("Beta");
    expect(
      screen.queryByTestId("agent-terminal-skill-badge-gamma")
    ).toBeNull();
  });

  it("кнопка «Скилы» disabled когда сессия не живая", () => {
    render(
      <SkillsAttachControl
        available={[]}
        sessionLive={false}
        isLoadingAvailable={false}
        isAttaching={false}
        onAttach={vi.fn()}
      />
    );

    const button = screen.getByTestId("agent-terminal-skills-attach-toggle");
    expect(button.hasAttribute("disabled")).toBe(true);
  });

  it("уже загруженный скил в попапе checked+disabled и не уходит в onAttach при попытке повторно", () => {
    const onAttach = vi.fn();
    render(
      <SkillsAttachControl
        available={[skill("alpha", { selected: true }), skill("beta")]}
        sessionLive
        isLoadingAvailable={false}
        isAttaching={false}
        onAttach={onAttach}
      />
    );

    fireEvent.click(screen.getByTestId("agent-terminal-skills-attach-toggle"));

    const inputs = screen.getAllByRole<HTMLInputElement>("checkbox");
    const alpha = inputs.find(
      (i) => i.getAttribute("aria-label") === "Skill alpha"
    );
    const beta = inputs.find(
      (i) => i.getAttribute("aria-label") === "Skill beta"
    );
    expect(alpha?.checked).toBe(true);
    expect(alpha?.disabled).toBe(true);
    expect(beta?.checked).toBe(false);
    expect(beta?.disabled).toBe(false);
  });

  it("submit зовёт onAttach только с новыми отмеченными скилами", () => {
    const onAttach = vi.fn();
    render(
      <SkillsAttachControl
        available={[skill("alpha", { selected: true }), skill("beta"), skill("gamma")]}
        sessionLive
        isLoadingAvailable={false}
        isAttaching={false}
        onAttach={onAttach}
      />
    );

    fireEvent.click(screen.getByTestId("agent-terminal-skills-attach-toggle"));

    const beta = screen
      .getAllByRole<HTMLInputElement>("checkbox")
      .find((i) => i.getAttribute("aria-label") === "Skill beta");
    if (!beta) throw new Error("Skill beta checkbox not found");
    fireEvent.click(beta);

    fireEvent.click(screen.getByTestId("agent-terminal-skills-attach-submit"));

    expect(onAttach).toHaveBeenCalledTimes(1);
    expect(onAttach).toHaveBeenCalledWith(["beta"]);
  });

  it("неmaterializable скил недоступен для выбора и показывает reason", () => {
    render(
      <SkillsAttachControl
        available={[
          skill("alpha", { materializable: false, reason: "нет файлов" })
        ]}
        sessionLive
        isLoadingAvailable={false}
        isAttaching={false}
        onAttach={vi.fn()}
      />
    );

    fireEvent.click(screen.getByTestId("agent-terminal-skills-attach-toggle"));

    const alpha = screen
      .getAllByRole<HTMLInputElement>("checkbox")
      .find((i) => i.getAttribute("aria-label") === "Skill alpha");
    expect(alpha?.disabled).toBe(true);
    expect(screen.getByText("нет файлов")).toBeTruthy();
  });

  it("submit-кнопка disabled пока нет новых выбранных скилов", () => {
    render(
      <SkillsAttachControl
        available={[skill("alpha")]}
        sessionLive
        isLoadingAvailable={false}
        isAttaching={false}
        onAttach={vi.fn()}
      />
    );

    fireEvent.click(screen.getByTestId("agent-terminal-skills-attach-toggle"));

    const submit = screen.getByTestId("agent-terminal-skills-attach-submit");
    expect(submit.hasAttribute("disabled")).toBe(true);
  });
});
