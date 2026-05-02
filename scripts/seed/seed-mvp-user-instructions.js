// Seed MVP user instructions into Mongo collection `instructions`.
//
// Idempotent: re-running the script does not duplicate documents and does not
// overwrite existing user-edited text. Only inserts when the (user_id, scope, kind)
// triple is missing.
//
// Run:
//   mongosh "$MONGO_URL" scripts/seed/seed-mvp-user-instructions.js
// Or, for the local docker-compose setup:
//   mongosh "mongodb://localhost:27017/throne" scripts/seed/seed-mvp-user-instructions.js

const USER_ID = "mvp-user";
const SCOPE_USER = "user";

const WORK_TEXT = `C# / .NET:
- Общие свойства проектов держи в Directory.Build.props; версии NuGet — только в Directory.Packages.props.
- Используй primary constructor там, где это естественно; методы, возвращающие Task, называй с Async.
- В публичном API enum-like значения передавай строками в одном wire-формате; long для фронтового API отдавай строкой.
- DI выбирай по состоянию зависимости: stateless — Singleton, request/db/request-bound — Scoped, per-call mutable — Transient. Не инжекти IServiceProvider без реальной динамики.
- Ошибки наружу идут единым Problem Details contract; Application/Domain используют типизированный ApiException с кодом из ErrorCodes.

Архитектура:
- Граф проектов — DAG. Domain/Application не зависят от persistence, транспорта, ASP.NET, HTTP-клиентов и внешних драйверов.
- Contracts/Domain предпочитают record + sealed; internal по умолчанию, public — осознанное API-решение.
- Тесты модуля не используют internals соседнего модуля. InternalsVisibleTo — только для тестов этого же модуля.
- Shared/Platform содержит только то, что нужно всем и не имеет внешних зависимостей.

Frontend, если задача затрагивает UI:
- Light-first; dark mode может существовать, но не является дефолтом.
- Статусы рисуй семантическими токенами, не случайными hex.
- Иконки — lucide-react; DTO с backend — только генерённые из OpenAPI.
- Соблюдай FSD: не пропускай widgets, не накапливай page-слой сверх разумного размера.

Тесты:
- Для xUnit используй Fact/Theory с DisplayName на русском.
- Не используй Task.Delay в тестах; жди событие/состояние или внедряй IClock/TimeProvider.
- Unit-тесты не ходят в I/O. Интеграции проверяй на реальной зависимости через Testcontainers или эквивалент.
- Перед завершением работы запускай проектный verify/quality gate, если он есть.`;

const COMMON_TEXT = `Юзер ведёт работу на русском. Throne строится для других проектов и догфудится на самом себе. Минимально достаточная полнота важнее продуктовой полноты — стартуем с самого маленького полезного среза и расширяем по фактическому спросу.`;

const seeds = [
  { kind: "common",      text: COMMON_TEXT },
  { kind: "work",        text: WORK_TEXT },
  { kind: "new_project", text: WORK_TEXT },
  { kind: "interview",   text: "" },
  { kind: "dream",       text: "" },
  { kind: "fix",         text: "" },
];

function objectIdHex() {
  // 24 hex chars, ObjectId-shaped.
  return new ObjectId().toString();
}

print(`==> Seeding user instructions for ${USER_ID}`);

const now = new Date();
let inserted = 0;
let skipped = 0;
for (const seed of seeds) {
  const existing = db.instructions.findOne({
    user_id: USER_ID,
    scope: SCOPE_USER,
    kind: seed.kind,
  });
  if (existing) {
    skipped += 1;
    continue;
  }

  const id = objectIdHex();
  db.instructions.insertOne({
    _id: id,
    scope: SCOPE_USER,
    user_id: USER_ID,
    kind: seed.kind,
    text: seed.text,
    current_version: 1,
    created_at: now,
    updated_at: now,
  });
  db.text_versions.insertOne({
    _id: UUID().toString().replace(/-/g, ""),
    owner_kind: "instruction",
    owner_id: id,
    version: 1,
    kind: "create",
    snapshot: seed.text,
    old_text: null,
    new_text: null,
    after_line: null,
    insert_text: null,
    changed_at: now,
    changed_by: "system",
  });
  inserted += 1;
}

print(`  inserted=${inserted} skipped=${skipped} (already present)`);
print(`==> Done.`);
