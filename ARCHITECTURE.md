# PersonalFinanceManager Architecture

PersonalFinanceManager is a Windows Forms application with explicit project boundaries and low runtime dependency count.

## Projects

- `PersonalFinanceManager.Core` contains domain models, import parsers, rule matching, salary calculations, and other code that does not depend on Windows Forms or SQL Server.
- `PersonalFinanceManager.Application` contains UI-facing workflow service contracts. It references Core, but not Data or App.
- `PersonalFinanceManager.Data` contains SQL Server access, repositories, database initialization, settings persistence, logging, data protection, and concrete workflow/application service implementations.
- `PersonalFinanceManager.App` contains Windows Forms views and the composition root.
- `PersonalFinanceManager.Tests` contains unit tests and source/package validation tests.

## Dependency direction

The intended dependency direction is:

```text
PersonalFinanceManager.App  -> PersonalFinanceManager.Application -> PersonalFinanceManager.Core
PersonalFinanceManager.App  -> PersonalFinanceManager.Data -> PersonalFinanceManager.Application -> PersonalFinanceManager.Core
PersonalFinanceManager.Tests -> projects under test
```

`PersonalFinanceManager.Core` must remain free of WinForms and SQL Server references. `PersonalFinanceManager.Application` must remain free of SQL Server, repository, and WinForms implementation references.

## SQL strategy

PersonalFinanceManager intentionally uses ADO.NET and `Microsoft.Data.SqlClient` instead of Entity Framework. Most SQL statements are stored as embedded `.sql` resources under:

```text
src/PersonalFinanceManager.Data/Sql/Queries/
```

Repositories and services load named SQL resources through `SqlResourceReader`. This preserves explicit SQL while keeping C# methods focused on orchestration, parameters, and mapping.

## Workflow services

Windows Forms views should depend on service contracts from `PersonalFinanceManager.Application` instead of repositories. Concrete workflow services in `PersonalFinanceManager.Data` coordinate repositories and lower-level services for UI scenarios such as imports, duplicate review, account maintenance, reports, salary analysis, transaction links, data health, and recurring bills.

This keeps UI code focused on presentation and event handling while keeping data access and business workflow behavior outside the form classes.

## Application identity

Public product identity values are centralized in `PersonalFinanceManager.Core.Application.ApplicationIdentity`. New code should use this identity for display titles, LocalAppData folder names, backup file prefixes, and safe connection-string templates. Namespace and assembly names intentionally remain `PersonalFinanceManager.*` unless the project is formally renamed.

## Configuration and privacy

Repository defaults do not contain private connection strings or personal financial data. Local settings are stored under `%LocalAppData%\\PersonalFinanceManager` and the connection string is protected using Windows DPAPI for the current Windows user.

## Schema strategy

Runtime migrations live under:

```text
sql/migrations/
```

A consolidated reference schema lives under:

```text
sql/baseline/create_schema.sql
```

The application executes migrations, not the baseline script. Migration execution is tracked in `dbo.SchemaMigration`.
