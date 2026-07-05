---
name: pr-description
description: Use when preparing a pull request in this repo to draft the PR description from the diff/commits and tie it to the Throne mission. Generates the required PR sections and checks the ≤600-line business-logic budget via skills/pr-description/bin/throne-pr-description. Local git-only, no Throne API and no LLM.
---

# Throne PR Description

Помогает человеку и агенту собрать описание PR по diff/коммитам и явно связать его с
миссией (**локальное приложение агентной инженерии вокруг Intent** — [readme.md#Миссия](../../readme.md#миссия)).
Правила и обязательные разделы PR — [CONTRIBUTING.md](../../CONTRIBUTING.md).

В отличие от `intent`/`review`/`dream`, этот skill **локальный**: читает только `git`,
не ходит в Throne API и не зовёт LLM. Заготовку заполняет человек/агент.

## CLI (`skills/pr-description/bin/throne-pr-description`)

```bash
skills/pr-description/bin/throne-pr-description scaffold [--base <ref>] [--head <ref>]
skills/pr-description/bin/throne-pr-description loc      [--base <ref>] [--head <ref>]
```

Дефолты: `--base origin/master` (fallback `master`), `--head HEAD`. Диапазон —
`base...head` (как показывает PR: от merge-base до head), учитываются только коммиты.

- **`scaffold`** — печатает markdown-заготовку описания PR в stdout: обязательные разделы
  (**Что**, **Зачем**, **Привязка к миссии**) и рекомендованные (**Решения**,
  **Ограничения**, **Проверка**). Раздел «Что» преднаполнен темами коммитов диапазона как
  подсказками; в футере — бюджет бизнес-логики. Плейсхолдеры (`<!-- ... -->`) заполняешь сам.
- **`loc`** — печатает бюджет бизнес-логики: сколько строк бизнес-логики против лимита
  **600**, разбивка исключённых категорий (тесты, сгенерированное, lockfiles, docs, ассеты)
  и total diff. Если лимит превышен — подсказывает декомпозировать PR.

## Что заполнить обязательно

- **Что** — существенные изменения, не построчный пересказ diff.
- **Зачем** — задача или проблема, которую решает PR.
- **Привязка к миссии** — как изменение служит миссии или почему нейтрально к ней.
  Не связывается — сигнал пересмотреть изменение.

## Инварианты

- Классификация «бизнес-логика vs исключения» в CLI зеркалит список исключений в
  `CONTRIBUTING.md`. Расходятся — правь оба; источник правды — `CONTRIBUTING.md`.
- Лимит 600 держат автор и ревьюер вручную — автоматической проверки в CI пока нет.
- Skill не открывает PR и не коммитит — только генерирует текст и считает бюджет.
