import { List, Network } from "lucide-react";

export type IntentsViewMode = "list" | "canvas";

interface IntentsViewModeToggleProps {
  mode: IntentsViewMode;
  onChange: (mode: IntentsViewMode) => void;
}

export function IntentsViewModeToggle({
  mode,
  onChange
}: IntentsViewModeToggleProps) {
  return (
    <div
      role="group"
      aria-label="Режим отображения intents"
      className="flex items-center gap-0.5 rounded-md border border-base-300 bg-base-100 p-0.5"
    >
      <ModeButton
        label="Список"
        active={mode === "list"}
        onClick={() => {
          onChange("list");
        }}
        icon={<List aria-hidden size={13} strokeWidth={2} />}
      />
      <ModeButton
        label="Канвас"
        active={mode === "canvas"}
        onClick={() => {
          onChange("canvas");
        }}
        icon={<Network aria-hidden size={13} strokeWidth={2} />}
      />
    </div>
  );
}

interface ModeButtonProps {
  label: string;
  active: boolean;
  onClick: () => void;
  icon: React.ReactNode;
}

function ModeButton({ label, active, onClick, icon }: ModeButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      title={label}
      className={[
        "flex h-6 items-center gap-1 rounded px-1.5 text-[11px] font-medium transition-colors",
        active
          ? "bg-primary/10 text-primary"
          : "text-base-content/60 hover:bg-base-200 hover:text-base-content"
      ].join(" ")}
    >
      {icon}
      <span>{label}</span>
    </button>
  );
}
