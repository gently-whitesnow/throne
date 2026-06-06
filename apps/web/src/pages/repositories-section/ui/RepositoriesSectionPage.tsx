import { FolderGit2 } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";

import { useRepositoriesQuery, type Repository } from "@/entities/repository";
import { AddRepositoryButton } from "@/features/add-repository";

function repositoryPath(repo: Pick<Repository, "provider" | "owner" | "repo">) {
  return `/repositories/${repo.provider}/${repo.owner}/${repo.repo}`;
}

/**
 * `/repositories` — registry of every known `(provider, owner, repo)` plus a
 * manual autocomplete add. Each row links to the repository page (schema map).
 */
export function RepositoriesSectionPage() {
  const navigate = useNavigate();
  const { data, isPending, error } = useRepositoriesQuery();

  return (
    <div className="w-full px-7 pb-12 pt-6">
      <section
        className="mx-auto flex max-w-4xl flex-col gap-6"
        aria-label="Репозитории"
      >
        <header className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-col gap-1">
            <h2 className="m-0 text-xl font-bold tracking-tight">
              Репозитории
            </h2>
            <p className="m-0 text-sm leading-relaxed text-base-content/70">
              Все известные репозитории и их карты схемы БД. Репозиторий
              появляется здесь автоматически при привязке к интенту или вручную.
            </p>
          </div>
          <AddRepositoryButton
            onAdded={(repo) => {
              void navigate(repositoryPath(repo));
            }}
          />
        </header>

        {isPending ? (
          <p className="m-0 text-sm text-base-content/60">Загрузка…</p>
        ) : error !== null ? (
          <p role="alert" className="m-0 text-sm text-error">
            Не удалось загрузить репозитории.
          </p>
        ) : data.length > 0 ? (
          <ul className="m-0 flex list-none flex-col gap-2 p-0">
            {data.map((repo) => (
              <li key={repo.full_name} className="m-0 p-0">
                <Link
                  to={repositoryPath(repo)}
                  className="flex items-center gap-3 rounded-md border border-base-300 bg-base-100 px-4 py-3 transition-colors hover:border-primary/40 hover:bg-base-200"
                >
                  <FolderGit2
                    aria-hidden
                    size={18}
                    className="flex-shrink-0 text-base-content/50"
                  />
                  <span className="flex min-w-0 flex-col gap-0.5">
                    <span className="truncate font-mono text-sm font-semibold">
                      {repo.full_name}
                    </span>
                    <span className="text-[11px] text-base-content/60">
                      {repo.provider}
                    </span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        ) : (
          <p className="m-0 rounded-md border border-dashed border-base-300 bg-base-200/40 p-6 text-center text-sm text-base-content/60">
            Пока нет ни одного репозитория. Добавьте первый через кнопку выше.
          </p>
        )}
      </section>
    </div>
  );
}
