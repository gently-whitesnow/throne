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
  gitProviderEntries,
  gitProviderHealthMeta,
  isProviderHealthy,
  providerHealthKey,
  useGitProvidersStatus,
  type GitProviderAuthStatus,
  type GitProviderStatusEntry
} from "@/entities/git-provider-status";
import { Button } from "@/shared/ui";

import { GitLabHostField } from "./GitLabHostField";

const GH_SETUP_DOCS_URL =
  "https://docs.github.com/en/github-cli/github-cli/quickstart";
const GLAB_SETUP_DOCS_URL = "https://docs.gitlab.com/cli/auth/login/";

/**
 * Settings → «Провайдеры Git».
 *
 * GitHub + GitLab CLI auth status (`gh`, `glab`). The GitLab card carries an
 * inline editor for the persisted `gitlab.host` — Mongo singleton (no env-var
 * fallback). Saving invalidates the providers-status query so the auth probe
 * runs against the new host on next refresh.
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
        providers={gitProviderEntries(status)}
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
  providers: GitProviderStatusEntry[];
}

function ProviderBody({ isLoading, error, providers }: ProviderBodyProps) {
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

  if (providers.length === 0 && isLoading) {
    return (
      <p className="m-0 text-sm text-base-content/60">Загружаем статус…</p>
    );
  }

  if (providers.length === 0) {
    return <p className="m-0 text-sm text-base-content/60">Нет данных.</p>;
  }

  return (
    <div className="grid gap-3 md:grid-cols-2">
      {providers.map((entry) =>
        entry.provider === "gitlab" ? (
          <GitLabProviderRow key={entry.provider} status={entry} />
        ) : (
          <ProviderStatusRow
            key={entry.provider}
            provider={entry.provider}
            status={entry}
          />
        )
      )}
    </div>
  );
}

interface ProviderStatusRowProps {
  provider: string;
  status: GitProviderStatusEntry;
}

function ProviderStatusRow({ provider, status }: ProviderStatusRowProps) {
  return (
    <div className="flex flex-col gap-3 rounded-md border border-base-300 bg-base-200/40 p-3">
      <ProviderHeader provider={provider} host={status.status.host} />
      <ProviderHealth status={status.status} />
    </div>
  );
}

function GitLabProviderRow({ status }: { status: GitProviderStatusEntry }) {
  return (
    <div className="flex flex-col gap-3 rounded-md border border-base-300 bg-base-200/40 p-3">
      <ProviderHeader provider="gitlab" host={status.status.host} />
      <GitLabHostField initial={status.status.host ?? ""} />
      <ProviderHealth status={status.status} />
    </div>
  );
}

interface ProviderHeaderProps {
  provider: string;
  host: string | null | undefined;
}

function ProviderHeader({ provider, host }: ProviderHeaderProps) {
  const label = providerLabel(provider);
  const cli = providerCli(provider);
  return (
    <div>
      <h4 className="m-0 text-sm font-semibold leading-tight">{label}</h4>
      <p className="m-0 mt-1 text-xs text-base-content/60">
        <code className="font-mono">{cli}</code>
        {host ? ` · ${host}` : ""}
      </p>
    </div>
  );
}

function providerLabel(provider: string): string {
  if (provider === "github") return "GitHub";
  if (provider === "gitlab") return "GitLab";
  return provider;
}

function providerCli(provider: string): string {
  if (provider === "github") return "gh";
  if (provider === "gitlab") return "glab";
  return provider;
}

function ProviderHealth({
  status
}: {
  status: GitProviderAuthStatus | undefined;
}) {
  const healthy = isProviderHealthy(status);
  const key = providerHealthKey(status);
  const meta = gitProviderHealthMeta[key];
  const description = describeProviderSession(status);

  return (
    <>
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
    </>
  );
}
