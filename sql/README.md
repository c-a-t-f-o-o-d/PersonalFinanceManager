# SQL schema layout

PersonalFinanceManager uses explicit SQL Server scripts and ADO.NET instead of an ORM.

## Runtime migrations

The application executes scripts from:

```text
sql/migrations/*.sql
```

Scripts are ordered by file name. The initializer tracks execution in:

```text
dbo.SchemaMigration
```

This tracking table stores the migration file name, schema marker, SHA-256 script hash, status, timestamp, and message. Existing development databases that already contain `dbo.SchemaMetadata` markers are treated as legacy-upgraded databases: migrations with matching schema markers are recorded as already applied and skipped.

The application intentionally ignores the `sql/baseline` folder during normal startup.

## Consolidated baseline

The consolidated baseline is located at:

```text
sql/baseline/create_schema.sql
```

This is intended for review, onboarding, and clean database provisioning. It is not automatically run by the app while historical migrations are retained.

## Why both exist

- `sql/migrations` preserves upgrade compatibility for existing development databases.
- `sql/baseline/create_schema.sql` gives reviewers a single entry point for understanding the current schema.
- `dbo.SchemaMigration` keeps startup deterministic and avoids repeatedly executing migrations that were already applied.

When the schema stabilizes further, the migration folder can be reduced to a baseline plus only new post-baseline migrations.
