# AITemplate

A **Clean-Architecture .NET service** template, packaged as a `dotnet new` template, with a
React SPA, GitHub Actions CI, and bundled Claude Code skills.

Vertical-slice CQRS over a lightweight in-process mediator (no MediatR), an optional React +
Vite frontend, and a Widgets loopengineering. Clone it directly, or install it as a `dotnet new`
template and scaffold renamed copies.

## What's inside

- **Layout** — `source/` + `tests/`, `net10.0`, `Directory.Build.props`, `GitVersion.yml`,
  `.config/dotnet-tools.json`, `.gitattributes`.
- **Clean Architecture** — `LoopEngineering.Domain` / `LoopEngineering.Application` (lightweight mediator +
  vertical slices) / `LoopEngineering.Infrastructure` / `LoopEngineering.Api` (minimal APIs serving `/health`,
  `/api/widgets`, OpenAPI).
- **React SPA** — `source/LoopEngineering.Api/ClientApp`, Vite + React 19, served by the API. Optional
  (drop it with `--client-framework none`).
- **Database** — PostgreSQL via EF Core; schema owned by EF Core migrations, `docker-compose` for
  local Postgres. See [knowledge/architecture/database.md](knowledge/architecture/database.md).
- **Auth** — JWT in `httpOnly` cookies (access + rotated refresh), ASP.NET Core Identity user
  store, role-based (RBAC). `/api/auth/*` endpoints + `/api/profile/me`; `ICurrentUser` port. See
  [knowledge/architecture/authentication.md](knowledge/architecture/authentication.md) and [knowledge/architecture/database.md](knowledge/architecture/database.md#authentication--authorization).
- **Tests** — `tests/`, xUnit + AwesomeAssertions, unit + integration coverage (incl. 401/403).
- **CI** (`.github/workflows/ci.yaml`) — GitVersion, restore, build, test + coverage.
- **PR lint** (`.github/workflows/pr-lint.yml`) — Conventional-Commit PR-title check.
- **Deploy** (`.github/workflows/deploy.yaml`) — build→migrate (`dotnet ef database update`)→roll out.
- **Dockerfile** — multi-stage, non-root (Node included for the SPA build).
- **Claude Code** — `CLAUDE.md` + skills in `.claude/skills/` (`dotnet-ci`, `add-dotnet-project`).

> Requires a **net10 SDK** (`global.json` pins SDK 10, prerelease allowed).

## Build & test

```bash
dotnet restore && dotnet tool restore
dotnet build -c Release
dotnet test  -c Release
```

## Run (backend + frontend)

### Dev mode — hot reload, two terminals

Vite serves the SPA and proxies API calls to the backend, so the browser sees a single origin.

```bash
# once: trust the local HTTPS cert
dotnet dev-certs https --trust
```

**Terminal 1 — backend** (https://localhost:7443):
```bash
dotnet run --project source/LoopEngineering.Api --launch-profile https
#  → /health, /api/widgets, /openapi/v1.json
```

**Terminal 2 — frontend** (http://localhost:5173):
```bash
cd source/LoopEngineering.Api/ClientApp
npm install        # first time only
npm run dev
```

Open **http://localhost:5173** → the **Widgets** page creates/lists widgets through the API.
Vite proxies `/api`, `/openapi`, `/health` to the backend (see `vite.config.ts`; override
the target with `VITE_API_PROXY`).

### Single process — prod-like (one port, no Vite)

`dotnet publish` builds the React app and the API serves it from `wwwroot`:

```bash
dotnet publish source/LoopEngineering.Api -c Release -o ./publish   # runs npm build → wwwroot
dotnet ./publish/LoopEngineering.Api.dll                            # SPA + API on one port
```

> **API only** (`--client-framework none`): no ClientApp — just run
> `dotnet run --project source/LoopEngineering.Api`; `/` redirects to `/openapi/v1.json`.

## Database & auth

PostgreSQL via EF Core. The schema (Widget loopengineering **+ ASP.NET Core Identity** tables) is owned by
**EF Core migrations**. In Development the API auto-applies migrations and seeds roles on startup.

```bash
docker compose up -d                       # local Postgres (empty; EF creates the schema)
dotnet run --project source/LoopEngineering.Api     # Development → auto-migrates + seeds admin@localhost

# add a migration after a model change:
dotnet ef migrations add Describe_change --project source/LoopEngineering.Infrastructure --startup-project source/LoopEngineering.Api
dotnet ef database update                 --project source/LoopEngineering.Infrastructure --startup-project source/LoopEngineering.Api
```

Auth is **JWT in `httpOnly` cookies**, **role-based**. ASP.NET Core Identity is the user store
(password hashing, roles). Register, log in (sets `access_token` + `refresh_token` cookies into a
jar), then send the jar — the browser does this automatically; curl uses `-b`/`-c`:

```bash
curl -X POST "http://localhost:5080/api/auth/register" \
  -H "Content-Type: application/json" -d '{"email":"admin@localhost","password":"Admin123!$"}'
curl -c cookies.txt -X POST "http://localhost:5080/api/auth/login" \
  -H "Content-Type: application/json" -d '{"email":"admin@localhost","password":"Admin123!$"}'
curl -b cookies.txt http://localhost:5080/api/widgets   # 200; POST needs the admin role
```

Full guide — migrations, auth, CI/CD, switching provider: **[knowledge/architecture/database.md](knowledge/architecture/database.md)**.

## Use it as a `dotnet new` template

```bash
dotnet new install .
dotnet new ai-service -n PaymentsApi                          # React SPA (default)
dotnet new ai-service -n PaymentsApi --client-framework none  # Web API only
```

`LoopEngineering` → your project name.

## Deploy

A skeleton `deploy.yaml` (manual `workflow_dispatch`) builds + pushes the image to GHCR, applies EF
Core migrations (`dotnet ef database update`, connection from the `DB_CONNECTION_STRING` secret),
then hits a placeholder roll-out job — wire in your target (ECS, k8s, App Runner, Cloud Run).
Nothing deploys on push/PR; CI stays light. See **[knowledge/architecture/database.md](knowledge/architecture/database.md)** for the
migrate-then-roll-out flow.

## Conventions

See [CLAUDE.md](CLAUDE.md) for the full directory map and house rules.
