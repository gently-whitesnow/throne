import { List, Network } from "lucide-react";

export type IntentsViewMode = "list" | "canvas";

interface IntentsViewModeButtonProps {
  mode: IntentsViewMode;
  onChange: (mode: IntentsViewMode) => void;
}

export function IntentsViewModeButton({
  mode,
  onChange
}: IntentsViewModeButtonProps) {
  const target: IntentsViewMode = mode === "list" ? "canvas" : "list";
  const label = target === "canvas" ? "Канвас" : "Список";
  const Icon = target === "canvas" ? Network : List;
  return (
    <button
      type="button"
      onClick={() => {
        onChange(target);
      }}
      aria-label={`Переключить режим: ${label.toLowerCase()}`}
      title={`Переключить на «${label}»`}
      className="relative flex h-6 items-center gap-1 rounded-md border border-base-300 bg-base-100 px-1.5 text-[11px] font-medium text-base-content/70 transition-[color,background-color,scale] before:absolute before:inset-x-0 before:-inset-y-2 before:content-[''] hover:bg-base-200 hover:text-base-content active:scale-[0.96]"
    >
      <Icon aria-hidden size={13} strokeWidth={2} />
      <span>{label}</span>
    </button>
  );
}
