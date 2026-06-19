export type ReviewFilesSortMode = "ai-recommended" | "natural";

interface ReviewFilesSortToggleProps {
  mode: ReviewFilesSortMode;
  onChange: (mode: ReviewFilesSortMode) => void;
}

export function ReviewFilesSortToggle({
  mode,
  onChange
}: ReviewFilesSortToggleProps) {
  return (
    <div
      role="group"
      aria-label="Порядок файлов"
      className="flex items-center gap-1 border-b border-base-300 bg-base-100 px-3 py-1.5"
    >
      <button
        type="button"
        onClick={() => {
          onChange("ai-recommended");
        }}
        aria-pressed={mode === "ai-recommended"}
        className={`flex-1 rounded px-2 py-1 text-[11px] font-medium ${
          mode === "ai-recommended"
            ? "bg-primary/10 text-primary"
            : "text-base-content/60 hover:text-base-content"
        }`}
        title="Порядок чтения, предложенный агентом"
      >
        AI порядок
      </button>
      <button
        type="button"
        onClick={() => {
          onChange("natural");
        }}
        aria-pressed={mode === "natural"}
        className={`flex-1 rounded px-2 py-1 text-[11px] font-medium ${
          mode === "natural"
            ? "bg-primary/10 text-primary"
            : "text-base-content/60 hover:text-base-content"
        }`}
        title="Алфавитный порядок"
      >
        По алфавиту
      </button>
    </div>
  );
}
