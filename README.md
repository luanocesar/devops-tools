# devops-tools
DevOps Tools, a project for Development Operations Tools.

## Overview
A web DevOps tool to deploy IIS applications (.NET backend, Angular frontend, ...). There is no
GitHub/Azure/GitLab integration: code comes from the developer's local repo through a client script.

Flow: create a project -> download client script from `/devops-tools/install-client` -> run it in the
project on your machine. The client runs the configured build commands (e.g. `dotnet publish`, `ng build`),
zips the output and uploads it. The server stops the backend app pool, replaces the application files
and starts the pool again (`scripts/deploy.bat`, uses `appcmd` and `robocopy`).

## Server installation (Windows 11 / Windows Server)
1. Install the .NET 8 SDK to publish the app.
2. Run `scripts\install-server.bat [targetDir]` (default `C:\inetpub\devops-tools`). This publishes the ASP.NET Core app and copies the IIS deployment script.
3. Install the .NET 8 Hosting Bundle and configure IIS to host `server\DevOpsTools.dll` under `/devops-tools`.
   The application can also run directly with `dotnet server\DevOpsTools.dll`; set `ASPNETCORE_URLS` as needed.

## Creating a project
`POST /devops-tools/api/projects` with JSON:
```json
{ "name": "myapp", "siteName": "Default Web Site", "backendAppPool": "MyApiPool",
  "clientBuildCommands": ["dotnet publish -c Release -o publish\\api", "ng build"],
  "clientOutputPath": "publish",
  "apps": [{ "name": "api", "targetPath": "C:\\inetpub\\myapp\\api", "type": "dotnet" }] }
```
The response contains `installUrl` (`/devops-tools/install-client?project=...&token=...`).

## Client installation (developer machine)
Download the `.bat` from `installUrl` into the project root and run it (needs PowerShell and `curl.exe`,
included in Windows 10/11 and Server 2019+). Each app's files are expected in `<clientOutputPath>\<app name>`.

## Development
Build with `dotnet build server\DevOpsTools\DevOpsTools.csproj`. Set `DEVOPS_DATA_DIR` to choose a
persistent project-data directory. The deploy script uses IIS `appcmd` and `robocopy`; configure
`DEVOPS_DEPLOY_SCRIPT` if it is not at `scripts\deploy.bat` relative to the application working directory.

After finishing changes run `scripts\update-install-files.bat` to refresh the committed `install\`
package; local installed copies and build artifacts are never committed.
