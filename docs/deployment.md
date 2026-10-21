# API deployment

The workflow in `.github/workflows/deploy.yml` runs on every push to `main` and can also be started manually. It runs the application tests, publishes the `Operia/Operia.csproj` API, then uses Web Deploy to update the existing MonsterASP.NET site. Pull requests to `main` run the tests without deploying. A failed test or publish stops deployment.

## One-time setup

1. In the MonsterASP.NET control panel, open the API website's **Deploy** page and enable **WebDeploy** on the Visual Studio access card.
2. In the `amrMostafa2018/Operia` GitHub repository, create a `production` environment. Restrict it to the `main` branch. Add these environment secrets using the values on the MonsterASP.NET card:

   | Secret | Value |
   | --- | --- |
   | `MONSTERASP_WEBSITE_NAME` | Site name, such as `site12345` |
   | `MONSTERASP_SERVER_COMPUTER_NAME` | Server URL, such as `https://site12345.siteasp.net:8172` |
   | `MONSTERASP_SERVER_USERNAME` | WebDeploy login |
   | `MONSTERASP_SERVER_PASSWORD` | WebDeploy password |

3. Keep the deployed site's `appsettings.json` and any `appsettings.Production.json` on MonsterASP.NET. The workflow removes these from its artifact and excludes them from synchronization. The deployment also preserves other server-only files, including uploads, because target deletion is disabled.
4. Verify the site's runtime is .NET 9 and its production connection string, JWT secret, CORS origins, and other integration settings are configured on the server. The API applies EF Core migrations when it starts; back up the production database before the first automated deployment.
5. Push the workflow to `main`, then inspect the GitHub Actions run and the site. A manual run is available from the Actions tab.

The tracked `Operia/appsettings.json` currently contains production credentials. Rotate the database password and JWT secret, move their replacements to MonsterASP.NET environment settings or a server-only configuration file, and remove the old values from the tracked file. The workflow does not publish this file, but existing Git history still exposes the old values; plan history cleanup with the repository owner after rotation.
