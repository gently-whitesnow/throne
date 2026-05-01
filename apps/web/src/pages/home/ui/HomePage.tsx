import { IntentBoard } from "@/widgets/intent-board";

export function HomePage() {
  return (
    <main className="page-shell home-page">
      <header className="home-page__header">
        <p className="home-page__eyebrow">Throne</p>
        <h1 className="home-page__title">
          Облако рабочих единиц для пользователя и агента
        </h1>
        <p className="home-page__lead">
          Стартовый экран фиксирует визуальный язык проекта и проверяет, что
          FSD-слои, TypeScript и сборка уже защищены quality gates.
        </p>
      </header>
      <IntentBoard />
    </main>
  );
}
