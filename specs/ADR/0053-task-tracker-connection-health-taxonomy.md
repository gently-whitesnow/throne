# ADR-0053: Three-state health соединения таск-трекера + персист

## Status

Accepted
Date: 2026-07-06
Related: [ADR-0029](0029-local-first-invariant-and-legacy-auth.md), [ADR-0045](0045-throne-extension-pattern.md), [ADR-0046](0046-open-wire-keys-for-extension-axes.md), [ADR-0052](0052-task-tracker-read-only-attached-context.md)

## Context

[ADR-0052](0052-task-tracker-read-only-attached-context.md) развернул ось трекеров в read-only attach, но классификация ошибок провайдера осталась двузначной и местами неверной. Kaiten-адаптер считал `403 = gone` (карточка исчезла) — при отзыве токена ВСЕ карточки помечались «недоступна навсегда» вместо честного «переподключите». Всё, что не 401/403, схлопывалось в один `Unreachable`: сеть, 5xx, timeout и тарифная стена 402 были неотличимы. А состояние соединения нигде не персистилось — Settings отдавал stale-зелёный `connected` по факту наличия строки, даже если трекер давно отвалился.

Различать нужно потому, что за каждым состоянием стоит своё действие оператора: «вне сети» — подождать (binding валиден), «переподключите» — сменить токен, «тариф» — разобраться с планом. Прятать их друг за другом — значит либо терять состояние (уводить в gone/broken), либо давать неверный совет.

## Subdomain classification

Generic, impl-volatile. Health-таксономия провайдер-нейтральна (`TaskTrackerConnectionHealth` в Application), vendor-специфика (Kaiten HTTP-коды) не течёт в core — маппинг живёт в адаптере за портом ([ADR-0045](0045-throne-extension-pattern.md)/[ADR-0046](0046-open-wire-keys-for-extension-axes.md)). Карта — [specs/AGENTS.local.md → Subdomain map](../AGENTS.local.md#subdomain-map-volatility-frame).

## Volatility check

Essential — точность классификации и наблюдаемость здоровья соединения продиктованы продуктовым требованием (оператор должен видеть правильное следующее действие), не давлением харнеса. Часть — снятие остаточной ошибки прошлого решения (`403 = gone`), исправляется в самом маппинге, а не обходом.

## Decision

**Three-state таксономия.** `TaskTrackerConnectionHealth = { Connected, Offline, Auth, Blocked }`. Маппинг Kaiten HTTP → health: 401/403 → `Auth`, 402 → `Blocked` (отдельно, не прячем в offline), 5xx / транспорт / timeout → `Offline` (binding валиден, тихий backoff). `404` — не health соединения, а per-attachment `gone` (карточка удалена): `GetCardAsync` возвращает `null` только на 404; forbidden (403) больше НЕ null, а классифицированный `TaskTrackerConnectionException(Auth)`, чтобы отзыв токена не маскировался под «карточка исчезла».

**Персист health.** `task_tracker_connections` получил `last_status` / `last_error` / `last_checked_at`. Источники записи: upsert соединения, лёгкий фоновый re-probe (`TaskTrackerHealthProbeService`, медленный heartbeat, по умолчанию 5 мин), и исход card attach/refresh (самый честный сигнал — бьёт по трекеру на реальном действии оператора). Settings-list отдаёт последний персист-статус, а не зелёный по наличию строки.

**Контракт.** Wire-enum `TaskTrackerConnectionState = { not_configured, connected, auth, offline, blocked }` (было `invalid`/`unreachable`) — состояние соединения живёт в settings-контракте, потребляется UI трекеров. 402 в board-search отдаётся как `task_tracker.connection_blocked` (402), а не как generic 502.

## Consequences

### Positive

- Отзыв токена больше не хоронит карточки в `gone`; UI показывает правильное действие (переподключите / вне сети / тариф).
- Settings не врёт stale-зелёным — статус наблюдаем без ручного re-probe.
- Таксономия провайдер-нейтральна: новый адаптер поставляет свой HTTP→health маппинг, core не трогается.

### Negative / Risks

- Фоновый re-probe добавляет периодический трафик к трекеру; смягчено медленным дефолтом и полным отключением при `PollInterval <= 0`.
- `blocked` (402) — новое состояние в контракте и UI; последующие адаптеры без тарифной стены его просто не порождают.
