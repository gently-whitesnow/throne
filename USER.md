# USER — Universal Coding Conventions

Этот файл — портируемые правила кодинга, общие для всех моих проектов. Ничего проектно-специфичного здесь быть не должно.

## General

- Не должно быть файлов больше 300 строк, если больше — декомпозируй.
- При интеграции со сторонними сервисами используй websearch для уточнения актуальной документации.
- Не изобретать велосипеды, предлагать оператору альтернативы если они упростят поддержку/разработку/тестирование.
- Правило, не проверяемое билдом или CI, — не правило, а пожелание. Каждый принцип ниже должен иметь enforcement (csproj-граф / NetArchTest / Roslyn analyzer / fitness function / тест).

## C# / .NET

- Общие свойства (`TargetFramework`, `ImplicitUsings`, `Nullable`, etc) задавай в `Directory.Build.props` — не дублируй их в `.csproj`.
- Central Package Management: `ManagePackageVersionsCentrally=true`, все версии NuGet — только в `Directory.Packages.props`. В `.csproj` — `<PackageReference Include="..." />` без атрибута `Version`. CI-проверка (grep/analyzer) ловит `Version=` в `.csproj`. Одна версия пакета на solution — иначе diamond-конфликты и расхождение транзитивных зависимостей между модулями.
- Старайся в нагруженных частях возвращать кортеж (статус ошибки, ожидаемый результат), а не выбрасывать исключения.
- В API не отдавать enum, вместо этого используй строковое значение.
- Для enum-like API полей фиксируй один wire-формат (например `snake_case`) и на backend/frontend используй только его.
- Для фронтового API вместо long возвращай string.
- Используй primary конструктор.
- Если метод возвращает `Task`, в его названии должно быть `Async` (`SaveAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, etc.).
- Minimal API query-параметры примитивных типов (`bool`, `int`, …) **обязательны** по умолчанию — если фронт может не передавать параметр, ставь дефолт (`bool force = false`).
- DI scope выбирается по состоянию зависимости: stateless без shared mutable state → `Singleton`; per-request данные / `DbContext` / клиенты, привязанные к request scope → `Scoped`; per-call mutable state → `Transient`. Для типичной ASP.NET endpoint-зависимости дефолт — `Scoped`.
- Не инжекти `IServiceProvider` — используй прямые зависимости через конструктор. Service Locator допустим только при динамическом resolve (например, по ключу из конфига).

## React / Frontend (когда есть фронт)

- **Light-first**: dark поддерживается, но дефолт — светлая тема.
- Для статусов используй семантические цвета (success/warning/error/info), не хардкодь hex.
- Иконки — только `lucide-react`.
- DTO с бэка — только генерённые из OpenAPI, не ручная типизация.
- FSD: не пропускай слой `widgets`. Если в `pages/X/` больше 15 файлов — режь на widgets/features, не накапливай.

## Architecture — universal principles

Применяются всегда, независимо от стиля (clean architecture, vertical slice, modular monolith):

- Граф зависимостей между .csproj — DAG. Циклы запрещены, проверяй NetArchTest'ом с первого дня.
- Architecture-тесты пишутся **до** появления второго модуля или второго слоя, не после десятого. На пустом проекте — десять минут, на сложившемся — недели.
- Domain и Application не ссылаются на persistence/транспорт (`MongoDB.*`, `Npgsql.*`, `StackExchange.Redis`, `Microsoft.AspNetCore.*`, HTTP-клиенты). Architecture-test обязателен.
- `record` + `sealed` для всего в `Contracts`/`Domain`. `internal` по умолчанию, `public` — осознанное решение.
- Тесты модуля не достают до internals соседа. `InternalsVisibleTo` — только на тестовый проект **этого же** модуля.
- Дублирование двух кусков дешевле неверной абстракции. Выноси в общий код только при **третьем** независимом повторении (rule of three, Fowler).
- В `Shared`/`Platform.Abstractions` — только то, что нужно **всем** и не имеет внешних зависимостей. Доменные понятия одного модуля никогда не Shared.

## Modular monolith (применяется при ≥2 bounded contexts)

Когда в проекте появляется второй bounded context — включаются дополнительные правила:

- Слои строго иерархичны: L0 `Platform.Abstractions` (примитивы — `IDomainEvent`, `Result<T>`, `IClock`) → L1 платформенные сервисы (orchestration, realtime, integrations) → L2 бизнес-модули → L3 Api/composition root. L(n) видит только L(<n).
- Каждый бизнес-модуль = 4 проекта: `X.Contracts` (records + interfaces, public API), `X.Domain` (агрегаты, без внешних зависимостей), `X.Application` (use cases), `X.Infrastructure` (адаптеры).
- Кросс-модульные ссылки — **только** на `X.Contracts`. Никогда `Y.Application → X.Application`, никогда `Y.Domain → X.Domain`. Architecture-test обязателен.
- Доступ к persistence-хранилищу canonical-сущности — только из её owning `X.Infrastructure`.
- Plugin/provider-контракты не ссылаются на бизнес-модули (бизнес на них — можно).
- Если модуль становится зависимостью для трёх+ других — он не бизнес, а платформа. Переноси на L1, имя — нейтральное (`Orchestration`, не `Jobs`).
- Новый кросс-модульный контракт → ADR в `specs/ADR/`. Implicit «вызывай мой public method» — антипаттерн.

### Composition root

- `Program.cs` не содержит регистраций конкретных модулей. Каждый модуль экспортирует `IModuleInstaller { Install(IServiceCollection); MapEndpoints(IEndpointRouteBuilder); }`. Корень — цикл по `ModuleCatalog.All`. Composition root не содержит имён конкретных модулей.
- Endpoint handler инжектит ≤ 3 зависимостей. Больше — сигнал на CQRS handler, агрегирующий внутри.
- API роуты — под `/api/v1/` с первого дня. Версионирование дешевле ввести один раз, чем мигрировать 80 эндпоинтов.
- REST route table — contract-first: path/method/DTO приходят из `specs/contracts/<module>/openapi.yaml` через codegen (`I<Module>Endpoints` + `Map<Module>GeneratedEndpoints`), а не из ручных `MapGet/MapPost`.
- Ручная часть API — только реализация `I<Module>Endpoints` и partial `Configure*` hooks для filters/validation; не дублируй route templates руками.

## Async / Domain events

- Доменное событие, на которое реагирует **другой** модуль, идёт через outbox в той же транзакции с состоянием. In-process synchronous dispatch — только внутри одного модуля.
- Long-running флоу (минуты+, ожидание агента, ручное подтверждение) — durable workflow (Temporal / DBOS / Durable Functions), не самописная связка `JobRepository + Service + Handler + RecoveryService`. Самописный recovery допустим один-два раза; третий — сигнал на движок.
- Concurrency на агрегатах — optimistic locking через version field, а не runtime `BusyException`. Семантика — на уровне типов.
- Plugin/provider-points для внешних реализаций (AI, агенты, интеграции) — с первого дня абстракция + манифест capabilities, не «класс с реализацией внутри Infrastructure».

## Cross-cutting from day 1

- Валидация — FluentValidation + pipeline behavior с первого endpoint'а, не самописные `Validate*` функции.
- Error responses — RFC 9457 Problem Details + URN error codes с первого endpoint'а.
- Throw-style API ошибки допустимы только через типизированный `ApiException`; наружу всё равно должен уходить тот же Problem Details contract.
- Единый реестр кодов ошибок: строковые константы в одном месте (`ErrorCodes`) + единый маппинг `code → HTTP status`. Application/Domain бросают только `ApiException(code, detail, extensions?)`; generic `throw new Exception(...)` в этих слоях запрещён architecture-тестом.
- Единственный writer Problem Details (централизованный `UseExceptionHandler` / middleware) формирует JSON и `type: "urn:<project>:error:<code>"`. Handler'ы не возвращают `Results.Problem(...)` и не лепят JSON руками — иначе разъезжаются формат, статусы и telemetry.
- OpenTelemetry + структурный Serilog — в день первого `Program.cs`. Дёшево сейчас, невозможно потом.

## Tests

- `[Fact(DisplayName = "...")]` с кратким описанием на русском для всех тестов.
- Не использовать `await Task.Delay(...)` в тестах. Жди явное событие/состояние или используй детерминированный `IClock`/`TimeProvider`.
- Integration-тесты — против реальной зависимости через Testcontainers (БД, очереди, кэши), а не через моки. Mocked-интеграция маскирует расхождения схем и миграций.
- Unit-тесты не ходят в I/O. Если для unit нужен мок драйвера БД — это integration-тест.

## Dependencies & secrets

- Обновление мажорной версии библиотеки — через ADR и отдельный PR. Минорные/патчевые — обычным PR с прогоном CI.
- Секретов в коде и в репозитории нет. Локально — `.env` (в `.gitignore`), в проде — secret store (Azure Key Vault / AWS Secrets Manager / HashiCorp Vault). PR-проверка на секреты включена с дня первого коммита (gitleaks / trufflehog).
- Lock-файлы (`packages.lock.json`, `pnpm-lock.yaml`, etc.) коммитятся. CI собирает с `--locked` / restore с lockfile.
