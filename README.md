# Operia

## Database configuration

The shared `Operia/appsettings.json` has no database connection. Development uses an ignored `Operia/appsettings.Development.json` file, so a local API cannot silently fall back to the production database. Copy `Operia/appsettings.Development.json.example` to that filename and supply a development-only SQL Server connection. The example uses a LocalDB instance named `OperiaDev`; on Windows, run `SqlLocalDB create OperiaDev` once before starting the API with the `Development` launch profile.

Production must supply `ConnectionStrings:DefaultConnection` through server-side `appsettings.json` or the `ConnectionStrings__DefaultConnection` environment variable. The deployment workflow preserves the server's configuration files. Rotate any earlier production credential that appeared in Git history.
