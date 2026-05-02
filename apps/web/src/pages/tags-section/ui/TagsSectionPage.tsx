import { TagsBoard } from "@/widgets/tags-board";

export function TagsSectionPage() {
  return (
    <div className="section-pane">
      <TagsBoard />
      <section className="detail-pane" aria-label="Подсказка">
        <div className="detail-pane__placeholder">
          <p>Управление тегами-проектами</p>
          <p className="detail-pane__hint-muted">
            Агент сам создаёт теги при привязке к репозиторию. Здесь можно
            переименовать или удалить.
          </p>
        </div>
      </section>
    </div>
  );
}
