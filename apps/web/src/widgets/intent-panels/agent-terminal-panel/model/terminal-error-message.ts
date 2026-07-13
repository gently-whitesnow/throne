import {
  errorMessage,
  httpErrorCode,
  httpErrorDetail,
  httpErrorExtensionString,
  httpErrorStatus,
  httpErrorTitle
} from "@/shared/lib";

const TMUX_UNAVAILABLE =
  "Возможность «Терминал агента» выключена или tmux недоступен.";

export function deriveTerminalSessionErrorMessage(err: unknown): string {
  const code = httpErrorCode(err);
  const status = httpErrorStatus(err);

  if (status === 422 && code !== undefined) {
    return terminalProblemMessage(err, code) ?? terminalProblemFallback(err);
  }

  return errorMessage(err, {
    base: "Не удалось запустить сессию",
    byStatus: {
      409: "Запуск отклонён: сессия уже запущена или тело интента изменилось с момента предпросмотра. Откройте модалку заново.",
      400: "Недопустимая комбинация вендора, модели и усилия.",
      404: "Интент не найден."
    }
  });
}

function terminalProblemMessage(err: unknown, code: string): string | null {
  switch (code) {
    case "repository.provider_not_authenticated":
      return repositoryProviderMessage(err);
    case "capability.disabled":
      return capabilityDisabledMessage(err);
    case "terminal.mode_invalid":
      return "Выбранный режим запуска недоступен для терминала агента.";
    case "terminal.clone_wait_timeout":
      return "Клоны репозиториев ещё не готовы. Дождитесь завершения клонирования и запустите терминал снова.";
    case "terminal.spawn_failed":
      return "Не удалось создать сессию терминала агента. Проверьте настройки терминала и повторите запуск.";
    case "terminal.run_preflight_blocked":
      return "Запуск заблокирован pre-flight проверкой. Исправьте найденные проблемы и повторите запуск.";
    case "terminal.native_provider_unavailable":
      return "Нативный терминал не настроен или не найден на этом хосте.";
    case "terminal.session_skill.unknown":
      return "Один из выбранных скилов терминала неизвестен.";
    case "terminal.session_skill.not_materializable":
      return "Один из выбранных скилов нельзя подготовить для этой сессии.";
    case "terminal.session_skill.vendor_unsupported":
      return "Один из выбранных скилов не поддерживает текущего вендора агента.";
    default:
      return null;
  }
}

function repositoryProviderMessage(err: unknown): string {
  const provider = httpErrorExtensionString(err, "provider") ?? "репозитория";
  const host = httpErrorExtensionString(err, "host");
  const target = host ?? provider;
  return `Провайдер ${provider} недоступен или не авторизован: ${target}. Проверьте доступ к ${target} (VPN/DNS) и авторизацию.`;
}

function capabilityDisabledMessage(err: unknown): string {
  const capability = httpErrorExtensionString(err, "capability");
  if (capability === "tmux") return TMUX_UNAVAILABLE;
  return "Нужная возможность выключена. Проверьте настройки возможностей и повторите запуск.";
}

function terminalProblemFallback(err: unknown): string {
  return (
    httpErrorDetail(err) ??
    httpErrorTitle(err) ??
    errorMessage(err, {
      base: "Не удалось запустить сессию"
    })
  );
}
