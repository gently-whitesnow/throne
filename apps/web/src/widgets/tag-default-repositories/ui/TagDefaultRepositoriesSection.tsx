import { FolderPlus, GitBranch, Trash2 } from "lucide-react";
import { useState } from "react";

import {
  useTag,
  type TagDefaultRepository,
  type TagDetail
} from "@/entities/tag";
import { errorMessage } from "@/shared/lib";
import { Button } from "@/shared/ui";

import { useDefaultRepositoriesMutation } from "../model/use-default-repositories-mutation";

import { AddDefaultRepositoryModal } from "./AddDefaultRepositoryModal";

interface TagDefaultRepositoriesSectionProps {
  tagId: string;
}

/**
 * Секция «Default repositories» в детальном представлении тега. Используется
 * Run pre-flight'ом (Slice 2): union по тегам интента, auto-bind недостающего,
 * параллельный partial clone. Whole-list replace через
 * `PUT /api/v1/tags/{id}/default-repositories` с `expected_version`.
 */
export function TagDefaultRepositoriesSection({
  tagId
}: TagDefaultRepositoriesSectionProps) {
  const tagQuery = useTag(tagId);
  const mutation = useDefaultRepositoriesMutation();
  const [modalOpen, setModalOpen] = useState(false);

  if (tagQuery.isPending) {
    return <p className="m-0 text-sm text-base-content/60">Загружаем тег…</p>;
  }

  if (tagQuery.isError) {
    const message = errorMessage(tagQuery.error, {
      base: "Ошибка загрузки тега"
    });
    return (
      <p role="alert" className="m-0 text-sm text-error">
        {message}
      </p>
    );
  }

  const tag = tagQuery.data;
  const repos = tag.default_repositories;
  const mutateError =
    mutation.error instanceof Error ? mutation.error.message : null;

  const handlePicked = (pick: TagDefaultRepository) => {
    if (isDuplicate(repos, pick)) return;
    const next: TagDefaultRepository[] = [...repos, pick];
    submit(tag, next);
  };

  const handleRemove = (repo: TagDefaultRepository) => {
    const next = repos.filter(
      (r) =>
        !(
          r.provider === repo.provider &&
          r.owner === repo.owner &&
          r.repo === repo.repo
        )
    );
    submit(tag, next);
  };

  const submit = (
    current: TagDetail,
    defaultRepositories: TagDefaultRepository[]
  ) => {
    mutation.mutate({
      tagId: current.id,
      expectedVersion: current.current_version,
      defaultRepositories
    });
  };

  return (
    <section
      aria-label="Default repositories"
      className="flex flex-col gap-3"
      data-testid="tag-default-repositories"
    >
      <header className="flex items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-2">
            <h3 className="m-0 text-base font-semibold leading-tight">
              Default repositories
            </h3>
            <span
              title="Версия тега (optimistic locking)"
              className="rounded bg-base-200 px-1.5 py-0.5 text-[11px] tabular-nums text-base-content/60"
            >
              v{String(tag.current_version)}
            </span>
          </div>
          <p className="m-0 max-w-[60ch] text-sm leading-relaxed text-base-content/70">
            Эти репозитории Throne автоматически биндит каждому интенту с тегом
            <code className="ml-1 rounded bg-base-200 px-1 py-0.5 font-mono text-xs">
              #{tag.name}
            </code>
            в момент нажатия Run.
          </p>
        </div>
        <Button
          aria-label="Добавить default repository"
          disabled={mutation.isPending}
          icon={<FolderPlus aria-hidden size={14} strokeWidth={2} />}
          onClick={() => {
            setModalOpen(true);
          }}
          variant="primary"
        >
          Добавить репозиторий
        </Button>
      </header>

      {repos.length === 0 ? (
        <p className="m-0 rounded-lg border border-dashed border-base-300 bg-base-100 px-4 py-3 text-sm text-base-content/60">
          У тега нет default-репозиториев. Добавьте, чтобы Run автоматически
          клонировал их для каждого интента с этим тегом.
        </p>
      ) : (
        <ul className="m-0 flex list-none flex-col gap-2 p-0">
          {repos.map((repo) => (
            <li
              key={`${repo.provider}:${repo.owner}/${repo.repo}`}
              className="flex items-center gap-3 rounded-lg border border-base-300 bg-base-100 px-4 py-2"
              data-testid={`tag-default-repo-${repo.owner}-${repo.repo}`}
            >
              <span
                aria-hidden
                className="inline-flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary"
              >
                <GitBranch size={14} strokeWidth={2} />
              </span>
              <span className="flex min-w-0 flex-col">
                <span className="truncate font-mono text-sm font-semibold">
                  {repo.owner}/{repo.repo}
                </span>
                <span className="text-xs text-base-content/60">
                  {repo.default_branch ?? "default branch — авто"}
                </span>
              </span>
              <Button
                aria-label={`Удалить ${repo.owner}/${repo.repo}`}
                className="ml-auto"
                disabled={mutation.isPending}
                icon={<Trash2 aria-hidden size={14} strokeWidth={2} />}
                onClick={() => {
                  handleRemove(repo);
                }}
              >
                Удалить
              </Button>
            </li>
          ))}
        </ul>
      )}

      {mutateError !== null ? (
        <p role="alert" className="m-0 text-xs text-error">
          Не удалось сохранить список: {mutateError}
        </p>
      ) : null}

      <AddDefaultRepositoryModal
        open={modalOpen}
        onClose={() => {
          setModalOpen(false);
        }}
        onPicked={handlePicked}
      />
    </section>
  );
}

function isDuplicate(
  repos: readonly TagDefaultRepository[],
  pick: Pick<TagDefaultRepository, "provider" | "owner" | "repo">
): boolean {
  return repos.some(
    (r) =>
      r.provider === pick.provider &&
      r.owner === pick.owner &&
      r.repo === pick.repo
  );
}
