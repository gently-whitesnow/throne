import { useState } from "react";

import { TagDefaultRepositoriesSection } from "@/widgets/tag-default-repositories";
import { TagsBoard } from "@/widgets/tags-board";

export function TagsSectionPage() {
  const [selectedTagId, setSelectedTagId] = useState<string | null>(null);

  return (
    <div className="grid h-screen max-md:grid-cols-1 max-md:grid-rows-[minmax(180px,40vh)_1fr] md:grid-cols-[320px_1fr]">
      <TagsBoard selectedTagId={selectedTagId} onSelectTag={setSelectedTagId} />
      <section
        className="flex h-screen min-w-0 flex-col overflow-y-auto max-md:h-auto"
        aria-label="Детали тега"
      >
        {selectedTagId === null ? (
          <div className="flex h-full flex-col items-center justify-center gap-1 text-sm text-base-content/60">
            <p className="m-0">Управление тегами-проектами</p>
            <p className="m-0 max-w-md text-center text-xs text-base-content/60">
              Выберите тег, чтобы настроить default-репозитории. Они
              автоматически биндятся каждому интенту с этим тегом при нажатии
              Run.
            </p>
          </div>
        ) : (
          <div className="flex flex-col gap-6 px-6 py-6">
            <TagDefaultRepositoriesSection tagId={selectedTagId} />
          </div>
        )}
      </section>
    </div>
  );
}
