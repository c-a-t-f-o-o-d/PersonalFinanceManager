# Installing PersonalFinanceManager

PersonalFinanceManager is currently distributed as a Windows desktop application package. The project still targets developers and technical users first, but the release package includes scripts that make local installation easier.

## Recommended evaluation path

1. Install the .NET Desktop Runtime that matches the release package, unless you are using a self-contained package.
2. Extract the release ZIP.
3. Run `install-personal-finance-manager.ps1` from PowerShell.
4. Start PersonalFinanceManager from the Start Menu.
5. On first launch, use **Setup > Use LocalDB Now** for a local evaluation database.
6. Optionally load demo data from Setup if the database is empty.


## Installing from a source checkout

The install script is primarily intended to be run from an extracted release package that already contains `PersonalFinanceManager.exe`.

If you are working from the source repository, create the release staging folder first:

```powershell
.\scripts\package-release.ps1
.\scripts\install-personal-finance-manager.ps1 -SourcePath .\artifacts\release\PersonalFinanceManager
```

For day-to-day development, you do not need to install the app. Run it directly instead:

```powershell
dotnet run --project .\src\PersonalFinanceManager.App\PersonalFinanceManager.App.csproj
```

## Manual run without installing

You can also run PersonalFinanceManager directly from the extracted release folder by launching:

```text
PersonalFinanceManager.exe
```

This is useful for quick evaluation.

## Local settings and data

User-specific settings are stored under:

```text
%LocalAppData%\PersonalFinanceManager\
```

Database location depends on the selected SQL Server option:

- LocalDB stores data in the local SQL Server LocalDB instance.
- Network SQL Server stores data wherever the configured SQL Server hosts the `PersonalFinanceManager` database.

## Uninstalling

Run:

```powershell
.\uninstall-personal-finance-manager.ps1
```

By default this removes installed application files and shortcuts only. It does not delete your database or local settings.

To also remove local PersonalFinanceManager settings, run:

```powershell
.\uninstall-personal-finance-manager.ps1 -RemoveUserData
```

Be careful: local settings may contain your protected connection string and application preferences.
