import type {
  GitProvider,
  GitRepositoryRef
} from "@/entities/repository-binding";

/**
 * Parse an SSH remote into a bindable coordinate. Used by the manual-entry path
 * when the autocomplete cannot surface a repository the operator has git access
 * to but no membership in. The result is bound through the regular `gh`/`glab`
 * flow (see slice decision Q2) — there is no raw `git clone <url>` path.
 *
 * Provider is derived from the host: `github.com` → github, anything else →
 * gitlab. Host/provider compatibility (github fixed to github.com, gitlab must
 * match the configured host) is validated downstream on the chip, not here.
 */
export type SshUrlParse =
  | { kind: "ok"; ref: GitRepositoryRef }
  | { kind: "error"; message: string };

// scp-like (`git@host:owner/repo.git`) and the rarer `ssh://git@host[:port]/...`.
const SCP_RE = /^git@([^:/\s]+):(.+)$/;
const SSH_RE = /^ssh:\/\/git@([^/:\s]+)(?::\d+)?\/(.+)$/;

export function parseSshUrl(raw: string): SshUrlParse {
  const trimmed = raw.trim();
  if (trimmed.length === 0) {
    return { kind: "error", message: "Введите SSH-URL." };
  }

  const match = SCP_RE.exec(trimmed) ?? SSH_RE.exec(trimmed);
  if (match === null) {
    return {
      kind: "error",
      message: "Ожидается формат git@host:owner/repo.git"
    };
  }

  const host = match[1].toLowerCase();
  const path = match[2]
    .trim()
    .replace(/\.git$/i, "")
    .replace(/^\/+|\/+$/g, "");
  const segments = path.split("/").filter((s) => s.length > 0);
  if (segments.length < 2) {
    return {
      kind: "error",
      message: "URL должен содержать owner/repo (для GitLab — namespace/repo)."
    };
  }

  const repo = segments[segments.length - 1];
  // GitLab namespaces can be nested (`group/subgroup`); everything but the leaf
  // is the owner. GitHub always has a single owner segment.
  const owner = segments.slice(0, -1).join("/");
  const provider: GitProvider = host === "github.com" ? "github" : "gitlab";

  return {
    kind: "ok",
    ref: {
      provider,
      host,
      owner,
      repo,
      full_name: `${owner}/${repo}`,
      // Empty → the server clones the upstream default branch on first clone.
      default_branch: "",
      private: false
    }
  };
}
