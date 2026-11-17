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
1. Install Node.js.
2. Run `scripts\install-server.bat [targetDir]` (default `C:\inetpub\devops-tools`).
3. Start with `node server\server.js` (`PORT` env var, default 3000), and expose through IIS
   (iisnode or a reverse proxy site) under `/devops-tools`.

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
`npm test` runs the tests. After finishing changes run `scripts\update-install-files.bat` to refresh
the committed `install\` package; local installed copies are never committed.
