import { ArrowLeft } from "lucide-react";
import { Link, useParams } from "react-router-dom";

import {
  useRepositoryQuery,
  type RepositoryCoordinate
} from "@/entities/repository";
import { httpErrorStatus } from "@/shared/lib";
import { RepositorySchemaDocument } from "@/widgets/repository-schema-document";

export function RepositoryDetailPage() {
  const params = useParams<{ provider: string; owner: string; repo: string }>();
  const coordinate: RepositoryCoordinate | null =
    params.provider && params.owner && params.repo
      ? { provider: params.provider, owner: params.owner, repo: params.repo }
      : null;

  const { data, isPending, error } = useRepositoryQuery(coordinate);
  const fullName =
    data?.full_name ??
    (coordinate ? `${coordinate.owner}/${coordinate.repo}` : "");
  const missing = httpErrorStatus(error) === 404;

  return (
    <div className="w-full px-7 pb-12 pt-6">
      <section className="mx-auto flex max-w-4xl flex-col gap-6">
        <header className="flex flex-col gap-2">
          <Link
            to="/repositories"
            className="inline-flex w-fit items-center gap-1 text-xs text-base-content/60 hover:text-base-content"
          >
            <ArrowLeft aria-hidden size={14} strokeWidth={2} /> Все репозитории
          </Link>
          <h2 className="m-0 font-mono text-xl font-bold tracking-tight">
            {fullName}
          </h2>
        </header>

        {coordinate === null ? (
          <p role="alert" className="m-0 text-sm text-error">
            Некорректный адрес репозитория.
          </p>
        ) : isPending ? (
          <p className="m-0 text-sm text-base-content/60">Загрузка…</p>
        ) : missing ? (
          <p role="alert" className="m-0 text-sm text-error">
            Репозиторий не зарегистрирован.
          </p>
        ) : error !== null ? (
          <p role="alert" className="m-0 text-sm text-error">
            Не удалось загрузить репозиторий.
          </p>
        ) : (
          <RepositorySchemaDocument
            coordinate={coordinate}
            fullName={fullName}
          />
        )}
      </section>
    </div>
  );
}
