# ADR-0005: Frontend foundation — Vite, React, FSD 2.0 quality harness

## Status

Accepted

## Context

`apps/web` появляется как будущий пользовательский интерфейс для Throne. На старте важнее не экран, а проверяемая форма проекта: фронтенд должен иметь такой же дисциплинированный quality harness, как backend, и не смешивать свои проверки с .NET-гейтами при локальной работе.

Альтернативы:

1. **Один общий npm script без отдельных shell-гейтов.** Быстро, но ломает уже сложившийся `scripts/quality` entrypoint и хуже подходит агентам.
2. **ESLint-only FSD enforcement.** Частично закрывает imports, но не проверяет структуру FSD как целое.
3. **Steiger + отдельный frontend verify (выбрано).** Даёт явный architecture gate для FSD 2.0 и сохраняет симметрию с backend quality harness.

## Decision

1. `apps/web` — Vite + TypeScript + React с изолированным `package.json`, `.editorconfig` и `pnpm-lock.yaml` внутри `apps/web`.
2. Frontend layout следует FSD 2.0 слоям:
   - `app`
   - `pages`
   - `widgets`
   - `features`
   - `entities`
   - `shared`
3. Импорты между слоями и структура FSD защищаются `steiger` с `@feature-sliced/steiger-plugin`. Steiger запускается отдельным обязательным gate `frontend architecture`.
4. На стартовом каркасе отключено только `fsd/insignificant-slice`: у минимального приложения каждый slice неизбежно имеет одного consumer, поэтому это правило будет полезно позже, но сейчас заставляет добавлять искусственный код.
5. Frontend quality harness:
   - `pnpm install --frozen-lockfile`
   - Prettier format check
   - ESLint
   - TypeScript typecheck
   - Steiger architecture check
   - Vitest
   - Vite production build
   - `pnpm audit --audit-level high`
6. Backend и frontend проверки разделены:
   - `scripts/quality/verify-backend.sh`
   - `scripts/quality/verify-frontend.sh`
   - общий `scripts/quality/verify.sh` запускает оба семейства или выбранный `--scope`.

## Consequences

### Positive

- FSD 2.0 правила проверяются с первого дня, до накопления UI-кода.
- Разработчик может гонять только затронутую часть проекта, но перед завершением хода остаётся единый `verify.sh`.
- Lock-файл фиксирует Node-зависимости так же явно, как backend фиксирует NuGet-зависимости, но не вытаскивает frontend package metadata в корень репозитория.

### Negative / Risks

- Steiger находится в beta, поэтому обновления нужно делать осознанно.
- `pnpm install --frozen-lockfile` добавляет стоимость к frontend verification, но делает дрейф зависимостей видимым сразу.
