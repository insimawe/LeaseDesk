# LeaseDesk: Build and Deployment Guide

This guide explains how to build LeaseDesk locally, package it with Docker, and deploy it to Render.

## 1. How the application is arranged

LeaseDesk is a single .NET 10 Blazor Server application.

- Frontend: Blazor Interactive Server and Bootstrap
- Backend: ASP.NET Core
- Database: SQLite
- Word documents: Open XML SDK
- Browser preview: `docx-preview`
- PDF generation: Microsoft Edge, Google Chrome, or LibreOffice
- Container: Docker
- Cloud host: Render

The browser keeps a live connection to the server. Render and any reverse proxy in front of the app must support WebSockets.

## 2. Install the tools

Install:

1. [.NET 10 SDK](https://dotnet.microsoft.com/download)
2. [Docker Desktop](https://www.docker.com/products/docker-desktop/)
3. [Git](https://git-scm.com/downloads)
4. Visual Studio 2026 or Cursor, if desired

Check the installations in PowerShell:

```powershell
dotnet --version
docker --version
git --version
```

Start Docker Desktop before running Docker commands.

## 3. Get the source code

```powershell
git clone https://github.com/insimawe/LeaseDesk.git
cd LeaseDesk
```

The repository root is the folder containing `Dockerfile` and `src`.

## 4. Build the application locally

Restore the NuGet packages:

```powershell
dotnet restore .\src\LeaseDesk\LeaseDesk.csproj
```

Compile the app:

```powershell
dotnet build .\src\LeaseDesk\LeaseDesk.csproj
```

Run it:

```powershell
dotnet run --project .\src\LeaseDesk\LeaseDesk.csproj --launch-profile http
```

Open:

```text
http://localhost:5015
```

Stop the app by pressing `Ctrl+C` in the terminal.

The local database is:

```text
src/LeaseDesk/leases.db
```

Entity Framework Core creates and upgrades the database automatically when the app starts.

## 5. Create a production build without Docker

```powershell
dotnet publish .\src\LeaseDesk\LeaseDesk.csproj -c Release -o .\publish
```

Run the published app:

```powershell
dotnet .\publish\LeaseDesk.dll
```

The project contains this setting:

```xml
<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>
```

It is required by this Docker build arrangement on .NET 10. Without it, `/_framework/blazor.web.js` can be missing and all Blazor buttons stop working.

## 6. Build the Docker image

Run these commands from the repository root:

```powershell
docker build -t leasedesk .
```

Confirm that the image exists:

```powershell
docker images leasedesk
```

The Dockerfile uses:

- `mcr.microsoft.com/dotnet/sdk:10.0` to build the app
- `mcr.microsoft.com/dotnet/aspnet:10.0` to run it
- Port `8080` by default
- `/app/data/leases.db` for SQLite

## 7. Run LeaseDesk in Docker

Create a named volume and start the container:

```powershell
docker run -d `
  --name leasedesk `
  -p 8080:8080 `
  -v leasedesk-data:/app/data `
  leasedesk
```

Open:

```text
http://localhost:8080
```

View logs:

```powershell
docker logs -f leasedesk
```

Stop the container:

```powershell
docker stop leasedesk
```

Start it again:

```powershell
docker start leasedesk
```

Remove the container:

```powershell
docker rm -f leasedesk
```

Removing the container does not remove the database because the database is in the `leasedesk-data` volume.

Do not run this unless the Docker database should be permanently deleted:

```powershell
docker volume rm leasedesk-data
```

## 8. Rebuild Docker after changing the code

```powershell
cd C:\Users\aquip-io\source\repos\Personal\LeaseDesk
docker rm -f leasedesk
docker build -t leasedesk .
docker run -d `
  --name leasedesk `
  -p 8080:8080 `
  -v leasedesk-data:/app/data `
  leasedesk
```

The volume survives the rebuild, so the Docker database remains.

## 9. Push production-ready code to GitHub

A safe workflow is:

```powershell
git switch -c feature/my-change
git add .
git commit -m "Describe the change"
git push -u origin feature/my-change
```

Open a pull request on GitHub, review and test it, and then merge it into `main`.

Render is configured to deploy `main` automatically. Therefore:

```text
Merge into main -> Render builds Docker -> Render deploys production
```

Treat `main` as production-ready code. Avoid pushing unfinished work directly to it.

## 10. Create the Render web service

1. Sign in at [dashboard.render.com](https://dashboard.render.com/).
2. Select **New**.
3. Select **Web Service**.
4. Connect GitHub.
5. Select `insimawe/LeaseDesk`.
6. Use these settings:

```text
Name: LeaseDesk
Language: Docker
Branch: main
Dockerfile Path: Dockerfile
Docker Build Context: .
Docker Command: leave empty
Auto-Deploy: On Commit
```

The entrypoint reads Render's `PORT` variable automatically. Do not set a fixed Render port.

## 11. Choose the Render database option

### Free option: temporary SQLite database

Select the free compute plan and do not attach a disk.

This is suitable for demonstrations only. Render's filesystem is ephemeral. Contracts can disappear after a deploy, restart, or service replacement.

The free service also sleeps after inactivity. Its first request after sleeping can take approximately 50 seconds.

### Paid option: persistent SQLite database

Select a paid web-service plan and add a persistent disk:

```text
Mount path: /app/data
Size: 1 GB
Instances: 1
```

The application writes its database to:

```text
/app/data/leases.db
```

Keep one service instance. SQLite and a Render persistent disk are not designed to be shared by multiple service instances.

Render charges separately for the web service and disk. Check current prices before creating them.

## 12. Deploy on Render

Select **Deploy web service**.

In the deployment logs, look for:

```text
Now listening on: http://[::]:10000
Your service is live
```

The public address resembles:

```text
https://leasedesk-xaac.onrender.com
```

Test:

1. Open the home page.
2. Select **New contract**.
3. Select **Horizon lease** or **Blank page**.
4. Save a contract.
5. Return to the contract list.

## 13. Deploy later changes

When a pull request is merged into `main`, Render automatically starts a new deployment.

To deploy manually:

1. Open the service in Render.
2. Open **Manual Deploy**.
3. Select **Deploy latest commit**.

No local `docker build` is required for a Render deployment. Render builds its own Docker image from GitHub.

## 14. Database migrations

LeaseDesk runs Entity Framework Core migrations during startup:

```csharp
await db.Database.MigrateAsync();
```

New database migrations are applied automatically after deployment.

Before deploying a database change:

1. Test the migration locally.
2. Back up important production data.
3. Merge the migration into `main`.
4. Check the Render startup logs.

## 15. Renderer and document limitations

The browser preview is included in the app:

- `docx-preview`
- JSZip
- `wwwroot/js/lease-preview.js`

It does not require a separate Render service.

Word generation also works inside the current container through the Open XML SDK.

PDF generation requires a supported browser or LibreOffice. The current Docker image does not install either one, so PDF export might fail on Render. Supporting PDF export requires adding a Linux-compatible renderer to the Docker image and updating the converter's Linux browser detection.

## 16. Troubleshooting

### Docker command cannot connect to the engine

Start Docker Desktop and wait until it reports that Docker is running.

### Container name is already in use

```powershell
docker rm -f leasedesk
```

Then run the container again.

### Port 8080 is already in use

Use another host port:

```powershell
docker run -d --name leasedesk -p 8090:8080 -v leasedesk-data:/app/data leasedesk
```

Open `http://localhost:8090`.

### Visual Studio cannot copy `LeaseDesk.exe`

Another local LeaseDesk process is running:

```powershell
Get-Process LeaseDesk -ErrorAction SilentlyContinue | Stop-Process -Force
```

Build again afterward.

### Links work, but Blazor buttons do nothing

Check:

```text
https://YOUR-SERVICE.onrender.com/_framework/blazor.web.js
```

It must return HTTP 200. A 404 usually means the .NET 10 Blazor static assets were omitted from the Docker publish. Keep this property in `LeaseDesk.csproj`:

```xml
<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>
```

Then rebuild and redeploy.

### Contracts disappear on Render

The service is using the free ephemeral filesystem. Upgrade to a paid plan and attach a disk at `/app/data`, or migrate the application from SQLite to a managed database.

### Render deploy does not start after a push

Check:

1. The commit reached GitHub's `main` branch.
2. Render's **Auto-Deploy** setting is **On Commit**.
3. Render still has permission to access the GitHub repository.
4. The Render **Events** and **Deploys** pages for errors.

## 17. Production checklist

- Build succeeds locally.
- Docker image builds.
- `/_framework/blazor.web.js` returns HTTP 200.
- Contract creation, update, and deletion work.
- Render deploy is Live.
- SQLite uses `/app/data/leases.db`.
- A persistent disk is attached if data must survive.
- Only one service instance is running when using SQLite.
- WebSockets work.
- Important data has a backup.
- PDF export is tested separately.
