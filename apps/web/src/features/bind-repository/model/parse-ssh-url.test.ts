import { describe, expect, it } from "vitest";

import { parseSshUrl, type SshUrlParse } from "./parse-ssh-url";

function expectOk(result: SshUrlParse): Extract<SshUrlParse, { kind: "ok" }> {
  if (result.kind !== "ok") {
    throw new Error(`expected ok, got error: ${result.message}`);
  }
  return result;
}

describe("parseSshUrl", () => {
  it("парсит github SSH-URL в координату github.com", () => {
    const { ref } = expectOk(
      parseSshUrl("git@github.com:gently-whitesnow/throne.git")
    );
    expect(ref.provider).toBe("github");
    expect(ref.host).toBe("github.com");
    expect(ref.owner).toBe("gently-whitesnow");
    expect(ref.repo).toBe("throne");
    expect(ref.full_name).toBe("gently-whitesnow/throne");
    expect(ref.default_branch).toBe("");
  });

  it("парсит gitlab SSH-URL с вложенным namespace", () => {
    const { ref } = expectOk(
      parseSshUrl("git@gitlab.ati.st:trucks/bugget/bugget-ui.git")
    );
    expect(ref.provider).toBe("gitlab");
    expect(ref.host).toBe("gitlab.ati.st");
    expect(ref.owner).toBe("trucks/bugget");
    expect(ref.repo).toBe("bugget-ui");
    expect(ref.full_name).toBe("trucks/bugget/bugget-ui");
  });

  it("определяет gitlab для не-github хоста", () => {
    const { ref } = expectOk(parseSshUrl("git@git.example.com:team/repo.git"));
    expect(ref.provider).toBe("gitlab");
  });

  it("принимает ssh:// форму с портом", () => {
    const { ref } = expectOk(
      parseSshUrl("ssh://git@github.com:22/owner/repo.git")
    );
    expect(ref.owner).toBe("owner");
    expect(ref.repo).toBe("repo");
  });

  it("работает без суффикса .git", () => {
    const { ref } = expectOk(parseSshUrl("git@github.com:owner/repo"));
    expect(ref.repo).toBe("repo");
  });

  it("ошибка на пустой строке", () => {
    expect(parseSshUrl("   ").kind).toBe("error");
  });

  it("ошибка на не-SSH строке", () => {
    expect(parseSshUrl("https://github.com/owner/repo").kind).toBe("error");
  });

  it("ошибка, когда нет owner/repo", () => {
    expect(parseSshUrl("git@github.com:repo.git").kind).toBe("error");
  });
});
