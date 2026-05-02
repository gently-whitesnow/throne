import { TagsBoard } from "@/widgets/tags-board";

export function TagsSectionPage() {
  return (
    <div className="grid h-screen max-md:grid-cols-1 max-md:grid-rows-[minmax(180px,40vh)_1fr] md:grid-cols-[320px_1fr]">
      <TagsBoard />
      <section
        className="flex h-screen min-w-0 flex-col overflow-hidden max-md:h-auto"
        aria-label="Подсказка"
      >
        <div className="flex h-full flex-col items-center justify-center gap-1 text-sm text-base-content/60">
          <p className="m-0">Управление тегами-проектами</p>
          <p className="m-0 max-w-md text-center text-xs text-base-content/60">
            Агент сам создаёт теги при привязке к репозиторию. Здесь можно
            переименовать или удалить.
          </p>
        </div>
      </section>
    </div>
  );
}
