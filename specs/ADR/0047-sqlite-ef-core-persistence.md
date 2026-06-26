# ADR-0047: SQLite / EF Core as the only persistence backend

## Status

Accepted
Date: 2026-06-26

## Context

Slice 4 moved the runtime default to SQLite, but the tree still carried the old
MongoDB implementation, provider switch, migration tool, compose plumbing, NuGet pins
and quality overrides. That left two active persistence stories in code while the
product target is local-first, single-operator storage.

Keeping both backends would preserve rollback comfort, but every new persistence
change would need duplicated repositories, duplicated tests and a permanent provider
flag. The migration path has already crossed the point where dual-write or hosted
migration code pays for itself.

## Subdomain classification

Generic sticky: persistence is infrastructure, and SQLite/EF Core is now the chosen
local-first implementation for this monolith.

## Volatility check

Accidental volatility: this ADR removes migration residue and prevents the old provider
switch from reintroducing two storage models.

## Decision

1. **SQLite/EF Core is the only backend.** `AddThroneInfrastructure` always wires
   `AddThroneEfCore`; `Persistence:Provider` is deleted. Runtime configuration is
   `Persistence:Sqlite:DataSource`, defaulting to `~/.throne/throne.db`.
2. **Driver and schema strategy.** The backend uses `Microsoft.EntityFrameworkCore.Sqlite`.
   `EfSchemaInitializer` runs `Database.MigrateAsync()` on startup, creates the parent
   directory and enables WAL with `PRAGMA journal_mode=WAL`.
3. **Concurrency convention.** Rows with a domain version (`current_version`) are
   configured as EF concurrency tokens and repository writes still use explicit CAS
   predicates plus affected-row checks. Singleton settings rows with versions follow the
   same rule.
4. **JSON policy.** JSON columns use the shared `EfJson.Options` and explicit
   `HasConversion` mappings. Lists and typed payloads stay in row POCOs; they are not
   hidden behind untyped string bags at the domain boundary.
5. **Attachment storage.** Intent attachments are SQLite BLOB rows. Upload/delete may
   still use `ExecuteOutsideTransactionAsync` so the domain-event decorator runs for
   non-transactional write paths, but no GridFS-specific API remains.
6. **Architecture guard.** `Throne.Architecture.Tests` contains a NetArchTest rule that
   backend assemblies must not depend on `MongoDB` namespaces. The Mongo driver,
   transitive CVE pins and migration tool are removed from NuGet and the solution.
7. **Quality budget.** `.quality/maintainability-budget.json` now calibrates the EF Core
   adapter layer and row POCOs. The old Mongo adapter/document/collection-name overrides
   are removed.

## Consequences

### Positive

- There is one persistence graph, one set of repositories and one integration-test
  surface.
- Local-first setup no longer needs Docker or a replica set for storage.
- NuGet audit noise from the Mongo driver and its transitive packages disappears.
- The architecture guard makes accidental driver reintroduction visible in unit tests.

### Negative / Risks

- Rollback to Mongo is no longer a runtime switch; it would be a code revert or a new
  migration.
- SQLite single-writer behavior is now the operational constraint. Long write
  transactions must stay short and explicit.
- Existing deployments that still hold Mongo-only data need an out-of-band export before
  taking this code.
