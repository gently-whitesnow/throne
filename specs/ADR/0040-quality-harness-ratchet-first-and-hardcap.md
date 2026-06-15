# ADR-0040: Quality harness — двухуровневый бюджет (ratchet + hardCap), Domain обратно в бюджет, duplicates → blocking

## Status

Accepted
Date: 2026-06-15
Related: [ADR-0028](0028-quality-harness-recalibration.md) (amends Decision §5 и executes Follow-up «Ratchet-first инверсия size-лимитов»), [ADR-0025](0025-domain-aggregate-style-rich-ddd.md) (amends механизм Domain-послабления), [ADR-0024](0024-intent-repository-binding-and-cli-providers.md) / [ADR-0032](0032-gitlab-provider.md) (precedent для NSwag structural-surface override)

## Context

ADR-0028 калибровал strict-профиль и оставил три явных follow-up'а: ratchet-first инверсия,
re-inline косметических сплитов и общая дисциплина «один лимит на измерение». При
ежедневной эксплуатации обнаружились ещё четыре конкретные дыры:

- **`backend-maintainability` фактически ratchet-only, а не absolute-cap.** Гейт всегда
  вызывается с `--baseline-snapshot`; ветка `maintainability_budget_check.py:829-832`
  возвращает результат только по регрессиям и не доходит до `mode == "blocking"` (:834).
  Любой новый файл, который сразу попал в baseline (например, на первом коммите), мог сидеть
  выше ratchet-лимита бесконечно: пока он там до коммита, не считается «новым», и проверки
  cap-а не происходит.
- **`backend-duplicates` advisory-only.** `gateMode: advisory` молча накапливал кросс-файловые
  дубликаты (1035 групп на момент аудита). Лексический детектор шумит на идиомах, поэтому
  сделать его blocking без baseline = заблокировать всё; но и оставлять advisory без
  ratchet = терять сигнал на новой копи-пасте.
- **Mongo-override `fileMaxLoc: 800`** был выставлен под legacy-каркас (`injection +
  optimistic-version retry + Builders<TDoc>`), копировавшийся между 11 репозиториями.
  Базового класса не было, поэтому 800 был honest reflection факта. После введения
  `MongoRepositoryBase<TDoc, TKey>` и миграции 14 файлов копи-паста ушла; держать 800
  больше нечестно.
- **`Throne.Domain` exclude'нут целиком.** ADR-0028 §5 выводил Domain из size/CC-бюджета,
  опираясь на единственный охранник — `DomainEncapsulationRulesTests` (NetArchTest, «no
  `internal set`»). NetArchTest ловит инвариант (encapsulation), но **не ловит разрастание
  агрегата** (LOC/CC/fan-out). При полностью открытом скоупе агрегат может расти
  бесконтрольно; имеющиеся файлы уже близко к комфортным потолкам (`Intent.cs` 331,
  `IntentRepositoryBinding.cs` 321, `DreamSession.Create` 109 LOC / CC 20). Сигнал нужен.

Дополнительно: `IntentRepositoriesController` сидел в `maintainability-baseline` как
`TYPE_PUB` (16 public override против лимита 12). Это NSwag-generated диспатчер: public
surface ≡ количество routes под одним OpenAPI tag. Тот же структурный прецедент, что у
`*CliProvider` (ADR-0024/0032) и базового `*Controller`-override.

## Decision

1. **Двухуровневый бюджет для `backend-maintainability`.** Каждый профиль теперь несёт две
   секции:

   - **`limits`** — ratchet-thresholds. Тугие. Existing-нарушения grandfathered через
     baseline-snapshot, новые валят билд. Только-вниз.
   - **`hardCap`** — absolute backstop. Существенно более лeniant. **Не может быть
     baselined**: любое нарушение `hardCap` валит билд в blocking mode независимо от
     baseline. Калиброван по P99 реально приземляющегося кода (`fileMaxLoc: 800`,
     `typeMaxLoc: 600`, `methodMaxLoc: 200`, `constructorMaxDependencies: 20`,
     `methodMaxCyclomaticComplexity: 30`, `typeMaxPublicMembers: 30`, `fileMaxFanOut: 40`).

   `maintainability_budget_check.py` после ratchet-проверки прогоняет тот же набор
   violations через `is_hardcap_violation` (per-glob effective hardCap из `pathOverrides`)
   и фейлит build, если в blocking-mode есть violations выше cap-а. `--write-baseline-snapshot`
   тоже отказывает, если есть hardCap-violations: cap нельзя baseline'ить.

   Реализует ADR-0028 §Alternatives «Инвертировать ratchet-first».

2. **`backend-duplicates` → blocking + ratchet snapshot.** `duplicates.gateMode: blocking` в
   `maintainability-budget.json`; quality.config.json получает `ratchet:
   .quality/duplicates-baseline.json`. Baseline снят single-shot (1035 групп), новые группы
   валят билд. Ratchet only-down: rebaseline руками отказывается без снижения количества.

3. **Mongo override `fileMaxLoc: 500`** (было: 800), `typeMaxLoc: 400` (было: 500),
   `methodMaxLoc: 100` (было: 120), `methodMaxCyclomaticComplexity: 20` (было: 25).
   Сопровождается рефактором: `MongoRepositoryBase<TDoc, TKey>` инкапсулирует
   collection/sessions/ById/FindOne/TryUpdateAsync; 11 Mongo-репо + 3 Store-файла
   мигрированы. Retry-loop в `MongoIntentStatusMutator` остаётся as-is (≤3 раза, backoff);
   базовый класс CAS-примитива `TryUpdateAsync` намеренно без built-in retry — это даёт
   caller'у право выбора retry-стратегии.

4. **`Throne.Domain` обратно в size/CC-бюджет** через pathOverride вместо `exclude`. Лимиты
   калиброваны на текущее состояние с запасом (`fileMaxLoc: 500`, `typeMaxLoc: 400`,
   `methodMaxLoc: 120`, `methodMaxCyclomaticComplexity: 20`, `constructorMaxDependencies:
   12`, `typeMaxPublicMembers: 25`, `fileMaxFanOut: 20`). Дают воздух для rich-DDD
   агрегатов; ловят дальнейшее разрастание. `DomainEncapsulationRulesTests` (NetArchTest)
   продолжает охранять инвариант «no `internal set`» — это отдельная ось от LOC/CC, обе
   нужны. **Amends ADR-0028 §Decision 5** (механизм Domain-послабления: exclude → bounded
   pathOverride) и **ADR-0025 §How** (механизм охраны Domain: NetArchTest + bounded
   pathOverride, не unbounded exclude).

5. **`IntentRepositoriesController`** получает pathOverride `typeMaxPublicMembers: 20`
   (current: 16). Тот же структурный прецедент, что у `*CliProvider` (ADR-0024/0032) и
   базового NSwag-controller-override: 16 routes под одним OpenAPI tag даёт one-class
   диспатчер; public surface ≡ route count. Split OpenAPI tag на 3 контроллера
   (`IntentRepositories` / `IntentRepositoryReview` / `IntentRepositoryPullRequest`)
   потребовал бы regen TS-клиента + churn 7 frontend-консьюмеров — **вынесен как
   follow-up**.

## Consequences

- Baseline `maintainability-baseline.json` стал пуст (3 entries → 0). Ratchet работает на
  свежем срезе; hardCap гарантирует, что новый файл, попавший в baseline на первом коммите,
  не может тихо сидеть выше cap-а.
- `backend-duplicates` теперь сигналит на новой копи-пасте при `pnpm verify`; шум
  legacy-дубликатов grandfathered. Ratchet only-down даёт траекторию очистки.
- Сигнал на разрастание Domain-агрегатов восстановлен. Дешевле, чем ждать, когда
  `Intent.cs` дорастёт до 800 LOC и потеряет навигабельность.
- Mongo-слой стал тоньше (~150 LOC сокращения после введения базы), per-репо файлы ≤389
  LOC; override `fileMaxLoc:500` дисциплинирует Mongo-слой к этому потолку.
- Один лимит на измерение сохранён (ADR-0028 §Consequences): `limits` для ratchet,
  `hardCap` для absolute — не два отдельных аналайзера, а две секции одного чекера с общим
  токенайзером.

## Alternatives rejected

- **Split OpenAPI tag `IntentRepositories` на 3 контроллера.** Архитектурно честнее, но
  цена — contracts churn (regen TS-client) и touches 7 frontend-файлов. Вынесено как
  отдельный follow-up интент: на момент этого прохода единичный pathOverride с rationale
  даёт тот же эффект на гейт без contracts-surgery.
- **Поднять Domain override до уровня «никогда не сработает».** Делает гейт декоративным —
  теряет сигнал. Текущие лимиты выбраны как «текущее состояние + умеренный headroom».
- **HardCap как отдельный конфиг-файл.** Лишний indirection: hardCap живёт рядом с
  `limits` в том же профиле, чтобы калибровка двух тиров была одним diff-ом.
- **Полностью убрать advisory-mode из чекера.** Оставлен на случай, когда профиль
  переключается в legacy emergency-rollback — не ломаем существующий выходной контракт.

## Follow-up (out of scope)

- Split OpenAPI tag `IntentRepositories` на 3 controller'а (Binding / PullRequest / Review).
- Ratchet-down `duplicates-baseline.json`: топ-30 групп с подсказкой extract candidates.
- Mongo override → ещё ниже (target `fileMaxLoc: 400`) после декомпозиции трёх крупнейших
  репо (`MongoIntentLinkRepository` 389, `MongoIntentAttachmentRepository` 330,
  `MongoIntentPinRepository` 313).
