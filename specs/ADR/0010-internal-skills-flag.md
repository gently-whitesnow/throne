# ADR-0010 — Internal skills flag в манифесте Throne

## Status

Accepted.

Дополняет [ADR-0007](0007-vendor-skill-launchers.md) (vendor skill launchers) и опирается на манифест из его update'а 2026-05-02 ([specs/manifest/throne-skills.yaml](../manifest/throne-skills.yaml)).

## Context

`/tdream` end-to-end и будущий self-learning loop требуют meta-tooling, которым пользуется только сам Throne: например, скилл `throne` (см. предстоящий Intent 7), который ведёт развитие самого продукта. Такие скиллы публиковать в чужие репозитории не нужно — они путают пользователей продукта и работают только при наличии репо Throne под рукой.

Сейчас манифест [specs/manifest/throne-skills.yaml](../manifest/throne-skills.yaml) — единый источник правды, из которого:

- backend собирает bundle через `ISkillManifestProvider`;
- `/api/v1/instructions/skills-tree` отдаёт дерево skill → bundle на страницу `/instructions`;
- `SkillLauncherParityTests` сверяет `.claude/skills/<name>/SKILL.md` и `.agents/skills/<name>/SKILL.md` байт-в-байт;
- будущий vendor installer (ADR-0007 §8) сгенерирует те же файлы в чужие репо.

Первая проблема — там, где installer проецирует манифест в `.claude/skills/` и `.agents/skills/` чужого репо, нужен механизм исключить отдельные скиллы. Альтернативы:

1. **Отдельный список `internal_skills:` рядом со `skills:`.** Дублирует структуру, требует поддерживать два массива в синхроне.
2. **Хранить internal skills в отдельном файле `throne-internal-skills.yaml`.** Делает дерево `/instructions` зависимым от двух манифестов, ломает «единый источник правды» из ADR-0007 update 2026-05-02.
3. **Опциональный флаг `internal: bool` на уровне `skills[]`.** Флаг едет вместе со скиллом, default-семантика очевидна (публичный скилл), installer фильтрует по полю.

Выбран вариант 3.

## Decision

1. На уровне `skills[]` манифеста [specs/manifest/throne-skills.yaml](../manifest/throne-skills.yaml) вводится опциональное поле `internal: bool` (default `false`). Семантика:
   - `internal: false` (или поле отсутствует) — публичный скилл; будущий installer проецирует его в `.agents/skills/<name>/` и `.claude/skills/<name>/` целевого репо.
   - `internal: true` — meta-tooling Throne; installer **пропускает** запись при генерации в чужие репо. Throne-репо при этом продолжает держать launcher-файлы локально для self-dogfooding.
2. Парсинг и runtime:
   - `SkillDefinition` ([apps/api/src/Throne.Application/Instructions/Manifest/SkillManifest.cs](../../apps/api/src/Throne.Application/Instructions/Manifest/SkillManifest.cs)) обогащается полем `bool Internal` со значением по умолчанию `false`.
   - `SkillManifestParser` парсит `internal` как опциональное `bool?`, default — `false`.
   - `YamlFileSkillManifestProvider` остаётся прежним (читает то же поле через парсер).
3. `/api/v1/instructions/skills-tree` и `GetSkillsTreeHandler` в этой итерации **не фильтруют** по `internal`: страница `/instructions` отображает все скиллы Throne одинаково, так как админская UI пока живёт только внутри dogfooding. Скрытие в UI и фильтрация в installer — отдельные изменения, см. «Не делаем здесь».
4. `SkillLauncherParityTests` остаются без изменений: пока installer'а нет, internal-скиллы лежат на диске Throne-репо точно так же, как публичные, и парность проверяется одинаково.
5. Обратная совместимость: текущий манифест валиден без изменений, потому что `internal` опциональное и default = `false`. Никаких изменений в `verify.sh`.

## Consequences

- Появляется единственный декларативный канал «этот скилл наружу не уходит». Installer (ADR-0007 §8), когда появится, читает `internal` и фильтрует — без отдельных списков и доп. файлов.
- Manifest version остаётся `1`: новое поле опциональное, существующие потребители его игнорируют.
- Throne-репо может смело добавлять meta-скиллы (например, `throne` под Intent 7) и не бояться, что они утекут в чужие проекты после генерации.
- Если в будущем понадобится скрыть internal-скиллы в UI Throne (например, отделить «продуктовые» и «meta» вкладки), это локальное расширение `GetSkillsTreeHandler` без изменения схемы манифеста.

## Не делаем здесь

- Сам vendor installer (его ещё нет; см. ADR-0007 §8).
- Скрытие internal-скиллов на странице `/instructions` (отдельный UI-патч поверх этого ADR).
- Добавление конкретного internal skill `throne` (Intent 7).
