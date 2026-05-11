export interface MinimalTextDelta {
  oldText: string;
  newText: string;
}

export function computeMinimalTextDelta(
  original: string,
  updated: string
): MinimalTextDelta | null {
  if (original === updated) return null;

  const prefixLen = commonPrefixLength(original, updated);
  const maxSuffix = Math.min(
    original.length - prefixLen,
    updated.length - prefixLen
  );
  let suffixLen = 0;
  while (
    suffixLen < maxSuffix &&
    original.charCodeAt(original.length - 1 - suffixLen) ===
      updated.charCodeAt(updated.length - 1 - suffixLen)
  ) {
    suffixLen += 1;
  }

  let leftCut = prefixLen;
  let rightCut = suffixLen;
  const maxIterations = leftCut + rightCut + 1;

  for (let i = 0; i < maxIterations; i += 1) {
    const oldText = original.slice(leftCut, original.length - rightCut);
    const newText = updated.slice(leftCut, updated.length - rightCut);

    if (oldText.length > 0 && occursExactlyOnce(original, oldText)) {
      return { oldText, newText };
    }

    const canGrowLeft = leftCut > 0;
    const canGrowRight = rightCut > 0;
    if (!canGrowLeft && !canGrowRight) {
      return { oldText, newText };
    }

    if (canGrowLeft && (!canGrowRight || leftCut <= rightCut)) {
      leftCut -= 1;
    } else {
      rightCut -= 1;
    }
  }

  return {
    oldText: original,
    newText: updated
  };
}

function commonPrefixLength(a: string, b: string): number {
  const max = Math.min(a.length, b.length);
  let i = 0;
  while (i < max && a.charCodeAt(i) === b.charCodeAt(i)) i += 1;
  return i;
}

function occursExactlyOnce(haystack: string, needle: string): boolean {
  const first = haystack.indexOf(needle);
  if (first === -1) return false;
  return !haystack.includes(needle, first + 1);
}
