const FALLBACK_NAME = "clipboard-image.png";

export function filesFromClipboard(clipboard: DataTransfer): File[] {
  const result: File[] = [];

  for (const item of Array.from(clipboard.items)) {
    if (item.kind !== "file" || !item.type.startsWith("image/")) continue;

    const file = item.getAsFile();
    if (!file) continue;

    result.push(
      file.name
        ? file
        : new File([file], FALLBACK_NAME, {
            type: file.type,
            lastModified: file.lastModified
          })
    );
  }

  return result;
}
