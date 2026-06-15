# ADR-0040: Quality harness — Mongo тонче, Domain обратно в бюджет, structural-surface override на IntentRepositoriesController

## Status

Accepted
Date: 2026-06-15
Related: [ADR-0028](0028-quality-harness-recalibration.md) (amends Decision §5 — механизм Domain-послабления), [ADR-0025](0025-domain-aggregate-style-rich-ddd.md) (amends механизм охраны Domain), [ADR-0024](0024-intent-repository-binding-and-cli-providers.md) / [ADR-0032](0032-gitlab-provider.md) (precedent для NSwag structural-surface override)

## Context

ADR-0028 калибровал strict-профиль и оставил три явных follow-up'а: ratchet-first инверсия,
re-inline косметических сплитов и общая дисциплина «один лимит на измерение». При
ежедневной эксплуатации обнаружились три точечные дыры, которые имеет смысл закрыть
малым, понятным изменением, не вводя нового механизма поверх существующего бюджета:

- **Mongo-override `fileMaxLoc: 800`** был выставлен под legacy-каркас (`injection +
  optimistic-version retry + Builders<TDoc>`), копировавшийся между 11 репозиториями.
  Базового класса не было, поэтому 800 был honest reflection факта. После введения
  `MongoRepositoryBase<TDoc, TKey>` и миграции 13 файлов копи-паста ушла; держать 800
  больше нечестно.
- **`Throne.Domain` exclude'нут целиком.** ADR-0028 §5 выводил Domain из size/CC-бюджета,
  опираясь на единственный охранник — `DomainEncapsulationRulesTests` (NetArchTest, «no
  `internal set`»). NetArchTest ловит инвариант (encapsulation), но **не ловит разрастание
  агрегата** (LOC/CC/fan-out). При полностью открытом скоупе агрегат может расти
  бесконтрольно; имеющиеся файлы уже близко к комфортным потолкам (`Intent.cs` 331,
  `IntentRepositoryBinding.cs` 321, `DreamSession.Create` 109 LOC / CC 20). Сигнал нужен.
- **`IntentRepositoriesController`** сидел в `maintainability-baseline` как `TYPE_PUB` (16
  public override против лимита 12). Это NSwag-generated диспатчер: public surface ≡
  количество routes под одним OpenAPI tag. Тот же структурный прецедент, что у
  `*CliProvider` (ADR-0024/0032) и базового `*Controller`-override.

Два более амбициозных follow-up'а — двухуровневый бюджет (`limits` ratchet + `hardCap`
absolute backstop) и перевод `backend-duplicates` из advisory в blocking с ratchet
snapshot — намеренно вынесены из этого ADR. Двухуровневый бюджет требует осторожной
калибровки P99-потолков и нового механизма в чекере; `backend-duplicates`-ratchet
консервирует 1000+ групп лексических совпадений (идиоматический шум) и может скрывать
реальный сигнал больше, чем ловить его на текущем объёме кода. Когда реальная боль от
этих дыр станет наблюдаемой, они вернутся отдельным ADR.

## Decision

1. **Mongo override понижен**: `fileMaxLoc: 500` (было: 800), `typeMaxLoc: 400` (было: 500),
   `methodMaxLoc: 100` (было: 120), `methodMaxCyclomaticComplexity: 20` (было: 25).
   Сопровождается рефактором: `MongoRepositoryBase<TDoc, TKey>` инкапсулирует
   collection/sessions/ById/FindOne/TryUpdateAsync; 13 файлов мигрированы. Retry-loop в
   `MongoIntentStatusMutator` остаётся as-is (≤3 раза, backoff); базовый класс
   CAS-примитива `TryUpdateAsync` намеренно без built-in retry — это даёт caller'у право
   выбора retry-стратегии.

2. **`Throne.Domain` обратно в size/CC-бюджет** через pathOverride вместо `exclude`. Лимиты
   калиброваны на текущее состояние с запасом (`fileMaxLoc: 500`, `typeMaxLoc: 400`,
   `methodMaxLoc: 120`, `methodMaxCyclomaticComplexity: 20`, `constructorMaxDependencies:
   12`, `typeMaxPublicMembers: 25`, `fileMaxFanOut: 20`). Дают воздух для rich-DDD
   агрегатов; ловят дальнейшее разрастание. `DomainEncapsulationRulesTests` (NetArchTest)
   продолжает охранять инвариант «no `internal set`» — это отдельная ось от LOC/CC, обе
   нужны. **Amends ADR-0028 §Decision 5** (механизм Domain-послабления: exclude → bounded
   pathOverride) и **ADR-0025 §How** (механизм охраны Domain: NetArchTest + bounded
   pathOverride, не unbounded exclude).

3. **`IntentRepositoriesController`** получает pathOverride `typeMaxPublicMembers: 20`
   (current: 16). Тот же структурный прецедент, что у `*CliProvider` (ADR-0024/0032) и
   базового NSwag-controller-override: 16 routes под одним OpenAPI tag даёт one-class
   диспатчер; public surface ≡ route count. Split OpenAPI tag на 3 контроллера
   (`IntentRepositories` / `IntentRepositoryReview` / `IntentRepositoryPullRequest`)
   потребовал бы regen TS-клиента + churn 7 frontend-консьюмеров — **вынесен как
   follow-up**.

## Consequences

- Baseline `maintainability-baseline.json` стал пуст (3 entries → 0). Domain-override
  калиброван так, что текущее состояние не порождает violations; новые превышения валят
  билд сразу, без grandfathering.
- Сигнал на разрастание Domain-агрегатов восстановлен. Дешевле, чем ждать, когда
  `Intent.cs` дорастёт до 800 LOC и потеряет навигабельность.
- Mongo-слой стал тоньше (~150 LOC сокращения после введения базы), per-репо файлы ≤389
  LOC; override `fileMaxLoc:500` дисциплинирует Mongo-слой к этому потолку.
- `backend-duplicates` остаётся advisory: лексический детектор шумит на идиомах, а на
  текущем объёме кода реальной копи-пасты немного — blocking без аккуратного baseline дал
  бы false positives, blocking с baseline на 1000+ групп консервировал бы шум. Сигнал
  читается глазами при необходимости через `pnpm verify`.

## Alternatives rejected

- **Двухуровневый бюджет (`limits` ratchet + `hardCap` absolute backstop).** Архитектурно
  закрывал бы дыру «гейт фактически ratchet-only под `--baseline-snapshot`», но требует
  калибровки P99-потолков и нового механизма поверх существующего чекера. Риск что-то не
  учесть выше выгоды на текущем размере кода; вынесено как отдельный ADR при появлении
  реальной боли.
- **`backend-duplicates` → blocking + ratchet snapshot.** Консервирует 1000+ групп
  лексических совпадений (большая часть — идиоматический шум: `using`-блоки, опции Mongo
  Builders, тестовые setup-секции). Сигнал на реально новой копи-пасте теряется в
  grandfathered-массе, а blocking без baseline валит билд на существующем коде. Текущий
  объём проекта не требует автоматизации этой проверки.
- **Split OpenAPI tag `IntentRepositories` на 3 контроллера.** Архитектурно честнее, но
  цена — contracts churn (regen TS-client) и touches 7 frontend-файлов. Вынесено как
  отдельный follow-up интент: на момент этого прохода единичный pathOverride с rationale
  даёт тот же эффект на гейт без contracts-surgery.
- **Поднять Domain override до уровня «никогда не сработает».** Делает гейт декоративным —
  теряет сигнал. Текущие лимиты выбраны как «текущее состояние + умеренный headroom».

## Follow-up (out of scope)

- Split OpenAPI tag `IntentRepositories` на 3 controller'а (Binding / PullRequest / Review).
- Mongo override → ещё ниже (target `fileMaxLoc: 400`) после декомпозиции трёх крупнейших
  репо (`MongoIntentLinkRepository` 389, `MongoIntentAttachmentRepository` 330,
  `MongoIntentPinRepository` 313).
- Двухуровневый бюджет (`limits` + `hardCap`) и/или `backend-duplicates` blocking-ratchet —
  отдельным ADR, когда станет ясно, что текущей дисциплины недостаточно.
