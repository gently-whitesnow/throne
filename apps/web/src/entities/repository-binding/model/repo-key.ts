import type { GitProvider, GitRepositoryRef } from "./types";

function defaultHost(provider: GitProvider): string {
  return provider === "github" ? "github.com" : "";
}

export function repoKey(
  provider: GitProvider,
  host: string | null | undefined,
  owner: string,
  repo: string
): string {
  const resolvedHost = (host ?? defaultHost(provider)).toLowerCase();
  return `${provider}|${resolvedHost}|${owner}/${repo}`;
}

export function refKey(ref: GitRepositoryRef): string {
  return repoKey(ref.provider, ref.host, ref.owner, ref.repo);
}

/**
 * Provider/host compatibility for a manually-entered repo. The GitLab clone runs
 * against the configured `Throne:GitLab:Host`, not the coordinate's host, so a
 * mismatch would silently clone the wrong (or no) repo — we reject it up front
 * on the chip instead. `gitlabHost` comes from `git-providers/status`.
 */
export function manualHostError(
  ref: GitRepositoryRef,
  gitlabHost: string | null
): string | null {
  if (ref.provider === "github") {
    return (ref.host ?? "github.com") === "github.com"
      ? null
      : "GitHub доступен только на github.com.";
  }
  if (gitlabHost === null || gitlabHost.trim().length === 0) {
    return "GitLab не настроен. Включите интеграцию в настройках и повторите.";
  }
  return (ref.host ?? "").toLowerCase() === gitlabHost.toLowerCase()
    ? null
    : `Host ${ref.host ?? "?"} не совпадает с настроенным GitLab (${gitlabHost}).`;
}
