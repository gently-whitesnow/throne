import { describe, expect, it } from "vitest";

import { HttpError } from "@/shared/api";

import { deriveTerminalSessionErrorMessage } from "./terminal-error-message";

function problemError(body: Record<string, unknown>, status = 422): HttpError {
  return new HttpError(status, "/terminal/run", "request failed", body);
}

describe("deriveTerminalSessionErrorMessage", () => {
  it("показывает причину недоступного git-провайдера вместо tmux-заглушки", () => {
    const message = deriveTerminalSessionErrorMessage(
      problemError({
        title: "API error",
        status: 422,
        detail:
          'ERROR Get "https://gitlab.ati.st/api/v4/user": dial tcp: lookup gitlab.ati.st: no such host.',
        code: "repository.provider_not_authenticated",
        provider: "gitlab",
        host: "gitlab.ati.st"
      })
    );

    expect(message).toBe(
      "Провайдер gitlab недоступен или не авторизован: gitlab.ati.st. Проверьте доступ к gitlab.ati.st (VPN/DNS) и авторизацию."
    );
  });

  it("оставляет текущую tmux-ошибку только для отключенной tmux capability", () => {
    const message = deriveTerminalSessionErrorMessage(
      problemError({
        title: "API error",
        status: 422,
        code: "capability.disabled",
        capability: "tmux"
      })
    );

    expect(message).toBe(
      "Возможность «Терминал агента» выключена или tmux недоступен."
    );
  });

  it("объясняет timeout ожидания клонов", () => {
    const message = deriveTerminalSessionErrorMessage(
      problemError({
        title: "API error",
        status: 422,
        code: "terminal.clone_wait_timeout"
      })
    );

    expect(message).toBe(
      "Клоны репозиториев ещё не готовы. Дождитесь завершения клонирования и запустите терминал снова."
    );
  });

  it("для неизвестного 422-кода показывает detail из ProblemDetails", () => {
    const message = deriveTerminalSessionErrorMessage(
      problemError({
        title: "API error",
        status: 422,
        detail: "Реальная причина из API.",
        code: "terminal.future_reason"
      })
    );

    expect(message).toBe("Реальная причина из API.");
  });
});
