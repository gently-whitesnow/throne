import { useCallback, useEffect, useState } from "react";

const STORAGE_KEY = "throne.canvas.showResolved";

function readInitial(): boolean {
  if (typeof window === "undefined") return false;
  return window.localStorage.getItem(STORAGE_KEY) === "1";
}

/**
 * Canvas-wide «show resolved (done) links» toggle, persisted to localStorage.
 * Off by default; the stored value is shared across contexts (not per-context).
 */
export function useShowResolved(): readonly [boolean, () => void] {
  const [showResolved, setShowResolved] = useState<boolean>(readInitial);

  useEffect(() => {
    if (typeof window === "undefined") return;
    window.localStorage.setItem(STORAGE_KEY, showResolved ? "1" : "0");
  }, [showResolved]);

  const toggle = useCallback(() => {
    setShowResolved((prev) => !prev);
  }, []);

  return [showResolved, toggle] as const;
}
