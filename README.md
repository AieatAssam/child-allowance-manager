# Allowance Manager

Allowance manager is an application for tracking child allowance and managing it semi-autonomously.

## Features

- Multi-tenancy for independent tracking of multiple households
- Allowance tracking for multiple children
- Support for parent actions
- Configurable automatic daily allowance top-up
- (_Optional_) Configurable birthday bonus allowance top-up
- Allowance suspension for _n_-days
- Visibility of all transactions
- Parent login via existing Microsoft account (personal or work)

## Requirements

- .NET 10 SDK (pinned to the 10.0.2xx feature band via `global.json`)
- PostgreSQL 14+ database
- Azure App Registration for OAuth2 authentication
- Blazor-compatible hosting (Azure App Service, etc.)

## Configuration

Configuration is done via `appsettings.json` or environment variables. Main settings that need to be populated are
* **ConnectionStrings__Postgres** - PostgreSQL connection string
* **AzureMonitor__ConnectionString** - connection string to Azure Monitor/Application Insights for logging
* **AzureMonitor__Enabled** - set to `true` to enable Azure Monitor outside production
* **Authentication__Microsoft__ClientId** and **Authentication__Microsoft__ClientSecret** - Azure App Registration credentials

The default allowed hosts are `localhost;127.0.0.1`; production deployments should override `AllowedHosts` with their real host names. Production database migrations run only when the app is started with `--migrate`; development applies migrations and seeds the local demo data automatically.

Allowance scheduling and birthday checks use UTC; keep the deployment and household expectations aligned with that boundary.

When deployed to Azure App Service, these settings are configured as Environment Variables.

## Tests

Run the PostgreSQL-backed test suite with Docker:

```bash
bash scripts/test-postgres.sh
```

The script starts a fresh PostgreSQL 16 container, runs the full solution test suite, and removes the container and volume afterward. CI runs the same PostgreSQL-backed suite.
