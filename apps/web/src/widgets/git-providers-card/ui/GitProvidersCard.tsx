import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  ExternalLink,
  GitBranch,
  RefreshCw,
  XCircle
} from "lucide-react";

import {
  describeProviderSession,
  gitProviderHealthMeta,
  isProviderHealthy,
  providerHealthKey,
  useGitProvidersStatus,
  type GitProviderAuthStatus
} from "@/entities/git-provider-status";
import { Button } from "@/shared/ui";

const GH_SETUP_DOCS_URL =
  "https://docs.github.com/en/github-cli/github-cli/quickstart";
const GLAB_SETUP_DOCS_URL = "https://docs.gitlab.com/cli/auth/login/";

/**
 * Settings → «Провайдеры Git».
 *
 * Показывает GitHub и GitLab CLI auth status. Индикаторы рисуются
 * семантическими токенами: success / warning / error.
 */
export function GitProvidersCard() {
  const { status, isLoading, error, refresh } = useGitProvidersStatus();

  return (
    <section
      aria-label="Провайдеры Git"
      className="flex flex-col gap-4 rounded-lg border border-base-300 bg-base-100 p-5"
    >
      <header className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <span
            aria-hidden
            className="inline-flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary"
          >
            <GitBranch size={18} strokeWidth={2} />
          </span>
          <div className="flex flex-col gap-1">
            <h3 className="m-0 text-base font-bold leading-tight">
              Провайдеры Git
            </h3>
            <p className="m-0 max-w-[60ch] text-sm leading-relaxed text-base-content/70">
              Статус локальных <code className="font-mono">gh</code> и{" "}
              <code className="font-mono">glab</code>: авторизация, host,
              аккаунт и выданные scopes.
            </p>
          </div>
        </div>
        <Button
          aria-label="Перепроверить статус провайдеров Git"
          icon={
            <RefreshCw
              aria-hidden
              size={16}
              strokeWidth={2}
              className={isLoading ? "animate-spin" : undefined}
            />
          }
          onClick={refresh}
        >
          {isLoading ? "Проверяем…" : "Проверить"}
        </Button>
      </header>

      <ProviderBody
        isLoading={isLoading}
        error={error}
        github={status?.github}
        gitlab={status?.gitlab}
      />

      <footer className="flex flex-wrap gap-3">
        <a
          className="inline-flex items-center gap-1.5 text-sm font-medium text-primary hover:underline"
          href={GH_SETUP_DOCS_URL}
          rel="noopener noreferrer"
          target="_blank"
        >
          Как настроить <code className="font-mono">gh</code>
          <ExternalLink aria-hidden size={14} strokeWidth={2} />
        </a>
        <a
          className="inline-flex items-center gap-1.5 text-sm font-medium text-primary hover:underline"
          href={GLAB_SETUP_DOCS_URL}
          rel="noopener noreferrer"
          target="_blank"
        >
          Как настроить <code className="font-mono">glab</code>
          <ExternalLink aria-hidden size={14} strokeWidth={2} />
        </a>
      </footer>
    </section>
  );
}

interface ProviderBodyProps {
  isLoading: boolean;
  error: Error | null;
  github: GitProviderAuthStatus | undefined;
  gitlab: GitProviderAuthStatus | undefined;
}

function ProviderBody({ isLoading, error, github, gitlab }: ProviderBodyProps) {
  if (error) {
    return (
      <p
        role="alert"
        className="m-0 flex items-start gap-2 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось получить статус провайдеров: {error.message}</span>
      </p>
    );
  }

  if (!github && !gitlab && isLoading) {
    return (
      <p className="m-0 text-sm text-base-content/60">Загружаем статус…</p>
    );
  }

  if (!github && !gitlab) {
    return <p className="m-0 text-sm text-base-content/60">Нет данных.</p>;
  }

  return (
    <div className="grid gap-3 md:grid-cols-2">
      <ProviderStatusRow name="GitHub" cli="gh" status={github} />
      <ProviderStatusRow name="GitLab" cli="glab" status={gitlab} />
    </div>
  );
}

interface ProviderStatusRowProps {
  name: string;
  cli: string;
  status: GitProviderAuthStatus | undefined;
}

function ProviderStatusRow({ name, cli, status }: ProviderStatusRowProps) {
  const healthy = isProviderHealthy(status);
  const key = providerHealthKey(status);
  const meta = gitProviderHealthMeta[key];
  const description = describeProviderSession(status);

  return (
    <div className="flex flex-col gap-3 rounded-md border border-base-300 bg-base-200/40 p-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h4 className="m-0 text-sm font-semibold leading-tight">{name}</h4>
          <p className="m-0 mt-1 text-xs text-base-content/60">
            <code className="font-mono">{cli}</code>
            {status?.host ? ` · ${status.host}` : ""}
          </p>
        </div>
      </div>
      <span
        data-testid="provider-health-pill"
        className={`inline-flex w-fit items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${meta.className}`}
      >
        {healthy ? (
          <CheckCircle2 aria-hidden size={14} strokeWidth={2.25} />
        ) : key === "offline" ? (
          <AlertTriangle aria-hidden size={14} strokeWidth={2.25} />
        ) : (
          <XCircle aria-hidden size={14} strokeWidth={2.25} />
        )}
        {meta.label}
      </span>

      {healthy ? (
        <dl className="grid grid-cols-[auto_1fr] items-baseline gap-x-3 gap-y-1.5 text-sm">
          <dt className="text-base-content/60">Аккаунт</dt>
          <dd className="m-0 font-mono">{status?.login ?? "—"}</dd>
          <dt className="text-base-content/60">Scopes</dt>
          <dd className="m-0">
            {status?.scopes && status.scopes.length > 0 ? (
              <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0">
                {status.scopes.map((scope) => (
                  <li
                    key={scope}
                    className="rounded border border-base-300 bg-base-200/60 px-1.5 py-0.5 font-mono text-xs"
                  >
                    {scope}
                  </li>
                ))}
              </ul>
            ) : (
              <span className="text-base-content/60">—</span>
            )}
          </dd>
        </dl>
      ) : (
        <p className="m-0 text-sm text-base-content/70">{description}</p>
      )}
    </div>
  );
}
