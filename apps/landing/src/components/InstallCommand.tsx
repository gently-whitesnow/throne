'use client';

import { useState } from 'react';

type Props = { command: string; copyLabel: string; copiedLabel: string };

export function InstallCommand({ command, copyLabel, copiedLabel }: Props) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(command);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      // clipboard may be unavailable (insecure context / denied) — leave UI as-is
    }
  }

  return (
    <div className="install-cmd">
      <code className="install-cmd__text">{command}</code>
      <button
        type="button"
        className="install-cmd__copy"
        onClick={copy}
        aria-label={copied ? copiedLabel : copyLabel}
      >
        {copied ? (
          <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false">
            <path
              d="M5 13l4 4L19 7"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        ) : (
          <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false">
            <rect
              x="9"
              y="9"
              width="11"
              height="11"
              rx="2"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.7"
            />
            <path
              d="M5 15V5a2 2 0 0 1 2-2h8"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.7"
              strokeLinecap="round"
            />
          </svg>
        )}
      </button>
    </div>
  );
}
