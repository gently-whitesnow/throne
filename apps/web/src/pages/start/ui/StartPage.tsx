import { ArrowRight, Plus } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";

import { CreateIntentButton } from "@/features/create-intent";
import { useThroneReadiness } from "@/features/throne-readiness";
import { ReadinessPanel } from "@/widgets/settings-cards/readiness-panel";

/**
 * `/start` — first-run экран: доводит свежую установку до рабочего состояния
 * (полный путь до Run) и упирается в CTA «Создать первый интент». Не блокирует —
 * в Throne можно уйти в любой момент по ссылке внизу (мягкий редирект, ADR-выбор
 * «Вариант A»: гейта нет, есть церемония).
 */
export function StartPage() {
  const navigate = useNavigate();
  const { ready } = useThroneReadiness();

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-5 py-8">
      <header className="flex flex-col gap-1.5">
        <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
          Первый запуск
        </p>
        <h1 className="m-0 text-2xl font-bold leading-tight">
          Доведём Throne до рабочего состояния
        </h1>
        <p className="m-0 max-w-[64ch] text-sm leading-relaxed text-base-content/70">
          Чтобы успешно запустить агента, нужно закрыть несколько пунктов ниже.
          У каждого незакрытого — команда, которую можно скопировать и выполнить
          в терминале, затем нажать «Перепроверить».
        </p>
      </header>

      <ReadinessPanel />

      {ready ? (
        <section
          aria-label="Создать первый интент"
          className="flex flex-col gap-3 rounded-xl border border-primary/30 bg-primary/5 px-5 py-5"
        >
          <div className="flex flex-col gap-1">
            <h2 className="m-0 text-lg font-bold leading-tight">
              Всё готово — создайте первый интент
            </h2>
            <p className="m-0 text-sm leading-relaxed text-base-content/70">
              Опишите задачу, выберите режим и запустите агента.
            </p>
          </div>
          <CreateIntentButton
            onCreated={(intent) => {
              void navigate(`/intents/${intent.id}`);
            }}
            trigger={({ open }) => (
              <button
                type="button"
                onClick={open}
                className="inline-flex w-fit items-center gap-2 rounded-lg bg-primary px-4 py-2.5 text-sm font-semibold text-primary-content transition-[background-color,scale] hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-primary active:scale-[0.97]"
              >
                <Plus aria-hidden size={16} strokeWidth={2.5} />
                Создать первый интент
              </button>
            )}
          />
        </section>
      ) : null}

      <Link
        to="/intents"
        className="inline-flex w-fit items-center gap-1.5 text-sm font-medium text-base-content/60 hover:text-base-content hover:underline"
      >
        {ready ? "Перейти в Throne" : "Всё равно перейти в Throne"}
        <ArrowRight aria-hidden size={14} strokeWidth={2} />
      </Link>
    </div>
  );
}
