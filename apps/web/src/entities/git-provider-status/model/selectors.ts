import type { GitProviderAuthStatus } from "./types";

/** True when the provider CLI reports a usable session. */
export function isProviderHealthy(
  status: GitProviderAuthStatus | undefined
): boolean {
  return status?.authenticated === true;
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
  return status.error ?? "CLI не авторизован";
}
