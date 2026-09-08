# Repository Guidelines

## Project Overview

ChaldeaBot is an Italian-language Telegram bot for the mobile game *Fate/Grand Order*, built as a `net10.0`/C# ASP.NET Core host. It lets players register a "Master" profile (friend code, server region, servant/support lists), link that profile to Telegram groups with per-chat notification settings, and fetch live FGO support-deck images through the third-party rayshift.io API. Dispatch, EF Core chat persistence, and hosted-lifecycle polling/webhook transports come from the sibling library `Telegram.Bot.Advanced`, referenced by an **absolute** `ProjectReference` at `C:\Git\personal\Telegram.Bot.Advanced\Telegram.Bot.Advanced\Telegram.Bot.Advanced.csproj`. Contributors need that sibling repo checked out at that exact path — the solution will not build otherwise, and Docker builds (context `src/`) cannot reach it either.

## Architecture & Data Flow

1. `src/Server/Program.cs` is the sole composition root: it loads configuration (command line → environment variables → user secrets, in that order — see Code Conventions), validates required settings, registers `MasterContext` (Npgsql), builds a `TelegramBotData` with a `DispatcherBuilder<MasterContext, Controller>`, selects one transport (`AddTelegramPolling()` or `AddTelegramWebhooks()` + `MapTelegramWebhooks()`), wires OpenTelemetry logging/tracing/metrics, then builds and runs the host.
2. The sibling library owns the concrete update transport (polling loop / webhook endpoint) and the dispatcher that matches an incoming `Update` against `HandlerDescriptor[]` built from filter-attribute metadata declared in this repo's controllers.
3. Controllers: `Controller` (`src/Server/TelegramController/Controller.cs`) is the shared base with common handlers (`/help`, `/reset`, `/servant`) and helpers (`SendMaster`, `SaveChanges`, Rayshift image retrieval). `PrivateController` is class-filtered to private chats and drives the multi-step Master registration/edit conversation via `TelegramChat.State` (`ConversationState.*` constants in `Controller.cs`). `GroupController` is class-filtered to groups/supergroups and handles linking a Master to a chat, admin checks, and per-chat `ChatSettings` notification toggles. `InlineController.cs` is entirely commented out and inactive — leave it byte-identical unless explicitly asked to revive it.
4. Handler methods combine `[CommandFilter]`, `[NoCommandFilter]`, `[ChatStateFilter]`, `[MessageTypeFilter]`, `[ChatTypeFilter]`, and `[CallbackCommandFilter]` attributes (filters are conjunctive per the library). Conversational (non-command) steps such as `GetNome`/`GetFriendCode`/`GetServer`/`SupportList`/`ServantList` are routed by chat state + message type, not by command text.
5. Data flows: EF Core (`MasterContext`, Postgres) persists `Master`, `RegisteredChat`, `ChatSettings`, plus the inherited library entities (`TelegramChat`, newsletters). `Rayshift` (`src/Rayshift`) is a thin HTTP client for the rayshift.io support-deck API, injected as `IRayshiftClient`. `DataScraper` (`src/DataScraper`) scrapes `fate-go.cirnopedia.org` for servant metadata via HtmlAgilityPack, cached in-memory for one hour (`IMemoryCache`, `/servant` handler). Neither Rayshift nor DataScraper touches EF Core directly — both are consumed by controllers as stateless services.

## Key Directories

- `src/Server/` — ASP.NET Core host (executable). `Program.cs` composition root; `TelegramController/` dispatch handlers; `Controllers/AdminController.cs` HTTP admin endpoints (webhook set/remove, EF migrate); `DbContext/` EF entities (`Master`, `RegisteredChat`, `ChatSettings`, `MasterContext`); `Infrastructure/` seed data + connection-string helpers; `Exceptions/InvalidParameterException.cs`; `Migrations/` EF migrations.
- `src/Rayshift/` — rayshift.io API client library (`RayshiftClient`, `IRayshiftClient`, `Models/`, `Utils/`). Consumed only by `Server`.
- `src/Rayshift.Test/` — the repo's only automated test project (NUnit, live integration tests against Rayshift).
- `src/DataScraper/` — Cirnopedia HTML scraper (`Scraper.cs`, `Models/ServantEntry.cs`, `Models/SkillEntry.cs`). Consumed only by `Server`.
- Repo root — `ChaldeaBot.sln` (4 projects, no solution folders), `docker-compose.yml`/`docker-compose.production.yml`/`docker-compose.dcproj`, `.github/workflows/build.yml`.

## Development Commands

Use a .NET 10 SDK.

```sh
dotnet restore ChaldeaBot.sln
dotnet build ChaldeaBot.sln --configuration Release --no-restore
dotnet test src/Rayshift.Test/Rayshift.Test.csproj
```

No lint/format command, Makefile, or scripts directory exists in this repo. There is no `global.json`; CI selects the SDK via `actions/setup-dotnet` (`dotnet-version: 10.0.x`).

Run the bot locally (needs `BotKey` in user secrets; `--no-launch-profile` avoids `launchSettings.json` injecting a `MODE` env var that silently overrides `--mode`, since environment variables are added *after* the mapped command-line source in `Program.cs`):

```sh
dotnet user-secrets set "BotKey" "<token>" --project src/Server/Server.csproj
dotnet run --no-launch-profile --project src/Server/Server.csproj -- --mode polling --migrate false --seed false
```

Webhook mode additionally requires `BaseUrl` (config) and `WebhookSecret` (user secrets/environment — never checked in); the host fails fast with a `Critical`-logged `InvalidParameterException` if either is missing.

Via Docker Compose (Postgres 16 + bot, build context `src/`, port `5000:5000`):

```sh
docker compose up --build
```

`docker-compose.production.yml` only overrides `ASPNETCORE_ENVIRONMENT=Production`; layer it over `docker-compose.yml`, it does not stand alone.

EF Core migrations — `dotnet ef` resolves via the nearby `.sln`, which contains `Rayshift.Test`, so **always** pass an explicit startup project:

```sh
dotnet ef migrations add <Name> --project src/Server/Server.csproj --startup-project src/Server/Server.csproj
dotnet ef migrations has-pending-model-changes --project src/Server/Server.csproj --startup-project src/Server/Server.csproj
```

Design-time host build requires `BotKey`/`BasePath` to resolve (validated before `builder.Build()`), so `BotKey` must exist in user secrets or be passed via `$env:BotKey=...`.

## Code Conventions & Common Patterns

- PascalCase for public types/methods/properties (`Master`, `GetSupportDeck`, `ServantListNotifications`); `I`-prefixed interfaces (`IRayshiftClient`); `_camelCase` private fields (`_logger`, `_context`, `_client`).
- Primary constructors on newer/host-adjacent types: `MasterContext(DbContextOptions<MasterContext> options)`, `PrivateController(...)`, `GroupController(...)`, `AdminController(...)`. Older domain types use explicit constructors: `Controller`, `Master`, `RegisteredChat`, `DataSeeder`, `RayshiftClient`, `Scraper`. Match the surrounding file's style rather than introducing a third pattern.
- **Nullable reference types and implicit usings are intentionally OFF** in `Server`, `Rayshift.Test`, and `DataScraper` — do not add `<Nullable>`/`<ImplicitUsings>` to those projects; it would produce a large warning surface unrelated to any specific change. `Rayshift` is the one exception with `<Nullable>enable</Nullable>` — keep new Rayshift code nullable-aware.
- `LangVersion` is `default` everywhere (no pinned language version) and `TreatWarningsAsErrors` is **not** set in this repo (unlike the sibling `Telegram.Bot.Advanced` library) — a build warning does not fail CI here.
- Async methods return `Task`/`ValueTask`; many app-level handlers omit the `Async` suffix (`Help`, `Add`, `GetServer`, `GetAllServants`) while lower-level API clients keep it (`RequestSupportLookupAsync`). Telegram.Bot 22.x calls use the modern non-`Async` names (`SendMessage`, `EditMessageText`, `AnswerCallbackQuery`, `SendMediaGroup`, `DeleteMessage`, `GetChatAdministrators`); framework-owned reply helpers (`ReplyTextMessageAsync`, `ReplyPhotoAsync`) keep their library-defined names.
- EF access is LINQ/async (`FirstOrDefaultAsync`, `FindAsync`, `ToListAsync`, `SaveChangesAsync`); state transitions are saved *before* sending the success reply.
- Logging: structured templates only — `logger.LogInformation("Nome ricevuto da @{Username}: {Text}", ...)`, never string interpolation (`CA2254`). Persistence failures funnel through `Controller.SaveChanges`: `DbUpdateException` logs + sends a fallback reply + returns `false`; other exceptions log, reply, then rethrow. Per-chat notification fan-out (`PrivateController`) catches `ApiRequestException` per recipient and logs a warning without aborting the loop — don't let one bad chat stop a broadcast.
- Configuration keys mix hierarchical JSON (`ConnectionStrings:Default`, `Rayshift:ApiKey`) and flat operational keys (`BotKey`, `BasePath`, `Endpoint`, `BaseUrl`, `WebhookSecret`, `MODE`, `MIGRATE`, `SEED`); environment-variable equivalents use the double-underscore convention (`Rayshift__ApiKey`). `Endpoint` defaults to `chaldeabot` and forms the webhook path segment (`/telegram/chaldeabot`), not the bot token.
- DI is constructor-first throughout; the one factory-style resolution is `IRayshiftClient` in `Program.cs`, built from a lambda that pulls `IConfiguration`/`ILogger<RayshiftClient>` out of the container.

## Important Files

- `src/Server/Program.cs` — composition root: config loading/validation, OpenTelemetry setup, transport selection (`MODE`), migration/seed startup, HTTP pipeline.
- `src/Server/TelegramController/Controller.cs` — shared base controller; `SendMaster`, `SaveChanges`, Rayshift image retrieval, `ConversationState`/callback-command constants.
- `src/Server/TelegramController/PrivateController.cs` — largest handler surface; multi-step Master registration/edit conversation state machine.
- `src/Server/TelegramController/GroupController.cs` — group linking, admin checks, `ChatSettings` notification toggles.
- `src/Server/Controllers/AdminController.cs` — HTTP admin endpoints (set/remove webhook with secret token, run EF migration).
- `src/Server/DbContext/MasterContext.cs` — extends the library's `TelegramContext`; table-splits app `ChatSettings` onto the library's `TelegramChats` table.
- `src/Server/appsettings.json` / `appsettings.{Development,Production}.json` — base config + per-environment `Logging:LogLevel` overrides.
- `src/Server/Properties/launchSettings.json` — `Server (webhook)` / `Server (polling)` profiles (both `Development`, both pass `--migrate true --seed true`).
- `ChaldeaBot.sln` — 4 projects only (`Server`, `Rayshift`, `Rayshift.Test`, `DataScraper`); no `docker-compose.dcproj` entry.
- `src/Server/Dockerfile`, `docker-compose.yml`, `docker-compose.production.yml` — container build/run topology.

## Runtime/Tooling Preferences

- .NET 10 SDK, `dotnet` CLI, SDK-style `PackageReference` projects — no central package management, no Node/npm tooling anywhere in this repo.
- Linux containers only (`DockerDefaultTargetOS=Linux`), Postgres 16 as the database.
- Server carries `UserSecretsId bf2996ee-...`; secrets (`BotKey`, `WebhookSecret`, `Rayshift:ApiKey`, connection strings) belong in `dotnet user-secrets` or environment variables — never in checked-in `appsettings*.json`.
- OpenTelemetry (not Serilog) provides logging/tracing/metrics; OTLP exporter destination comes from `OTEL_EXPORTER_OTLP_*` environment variables, not from any checked-in config.
- Do not add generated `bin/`/`obj/`, IDE files, or `docker-compose.credentials.yml`; all are `.gitignore`d.

## Testing & QA

- `src/Rayshift.Test` is the **only** automated test project in the solution — `Server` and `DataScraper` have zero test files. It uses NUnit 4.6.1 + `NUnit3TestAdapter` 6.3.0 + `Microsoft.NET.Test.Sdk` 18.9.0 on `net10.0`, with `Assert.That(...)` constraint-style assertions exclusively (no classic `Assert.AreEqual`/`Assert.IsNotNull`).
- `RayshiftClientTests` is a **live integration test**, not a hermetic unit test: it calls the real rayshift.io API and fetches real support-deck image URLs over HTTP. It requires `ApiKey` and `FriendCode` in user secrets for the `Rayshift.Test` project (separate `UserSecretsId` from `Server`) — set them before running, or the `[SetUp]` assertions fail immediately:
  ```sh
  dotnet user-secrets set "ApiKey" "<rayshift-api-key>" --project src/Rayshift.Test/Rayshift.Test.csproj
  dotnet user-secrets set "FriendCode" "<9-digit-friend-code>" --project src/Rayshift.Test/Rayshift.Test.csproj
  dotnet test src/Rayshift.Test/Rayshift.Test.csproj
  ```
- CI (`.github/workflows/build.yml`) restores and builds `ChaldeaBot.sln` on push/PR to `master` but **does not run tests** — the `dotnet test` step is present only as a comment. Treat any test run as a manual/local verification step, not a CI gate.
- When changing `Server` or `DataScraper` behavior, there is no existing automated suite to extend for regression coverage — prefer a throwaway smoke script or manual run per the root harness Verify step rather than inventing a new permanent test scaffold unless asked.
