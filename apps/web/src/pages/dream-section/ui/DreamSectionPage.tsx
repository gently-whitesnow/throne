import { DreamFuelMeter } from "@/widgets/dream-fuel-meter";
import { DreamRunsList } from "@/widgets/dream-runs-list";

export function DreamSectionPage() {
  return (
    <div className="w-full px-7 pb-12 pt-6">
      <section
        className="mx-auto flex max-w-5xl flex-col gap-6"
        aria-label="Dream loop"
      >
        <header className="flex flex-col gap-1.5">
          <h2 className="m-0 text-xl font-bold tracking-tight">Dream</h2>
          <p className="m-0 text-sm leading-relaxed text-base-content/70">
            Self-improvement loop для пользовательских инструкций. Сервер сам
            подсчитывает fuel и собирает контекст; агент через{" "}
            <code className="rounded bg-base-200 px-1.5 py-px font-mono text-xs">
              /tdream
            </code>{" "}
            предлагает правила, которые вы решаете применить или пропустить.
          </p>
        </header>
        <DreamFuelMeter />
        <DreamRunsList />
      </section>
    </div>
  );
}
