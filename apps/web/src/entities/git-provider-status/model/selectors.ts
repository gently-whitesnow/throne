import type {
  GitProviderAuthStatus,
  GitProvidersStatus,
  GitProviderStatusEntry
} from "./types";

export function gitProviderEntries(
  status: GitProvidersStatus | null | undefined
): GitProviderStatusEntry[] {
  return [...(status?.providers ?? [])];
}

export function findGitProviderStatus(
  status: GitProvidersStatus | null | undefined,
  provider: string
): GitProviderAuthStatus | undefined {
  return status?.providers.find((entry) => entry.provider === provider)?.status;
}

/** True when the provider CLI reports a usable session. */
export function isProviderHealthy(
  status: GitProviderAuthStatus | undefined
): boolean {
  return status?.authenticated === true;
}

export function providerHealthKey(
  status: GitProviderAuthStatus | undefined
): "ok" | "offline" | "broken" | "missing" {
  if (status?.authenticated) return "ok";
  if (status?.state === "offline") return "offline";
  if (status?.state === "missing") return "missing";
  return "broken";
}

/**
 * Short human-readable session descriptor for the settings card.
 * `login (scope, scope, ...)` when authenticated, otherwise the error message
 * from the underlying CLI (or a generic fallback if upstream didn't say).
 */
export function describeProviderSession(
  status: GitProviderAuthStatus | undefined
): string {
  if (status === undefined) return "Нет данных";
  if (status.authenticated) {
    const login = status.login ?? "—";
    const scopes = status.scopes ?? [];
    return scopes.length > 0 ? `${login} (${scopes.join(", ")})` : login;
  }
  if (status.state === "offline") {
    return status.error ?? "Git provider временно недоступен";
  }
  return status.error ?? "CLI не авторизован";
}
