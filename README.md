# AgentForge

AgentForge is a multi-agent AI workspace with an ASP.NET Core API, a React/TypeScript frontend, SQL Server persistence, configurable model providers, bounded agent tools, basic retrieval-augmented chat, and MCP/A2A integration points.

## What is implemented

- SQL Server EF Core schema and versioned migrations for agents, conversations/messages, knowledge chunks, and audit events.
- Durable agent and conversation APIs. The API applies migrations and seeds starter agents when `Database:ApplyMigrations` is enabled.
- Gemini, Ollama, and OpenAI chat adapters behind one provider abstraction. Credentials are read only from environment/user-secret configuration.
- Chat history, JSON-structured model output validation, source retrieval, and an allow-listed tool loop for knowledge search, agent discovery, and bounded delegation.
- Plain-text/Markdown/CSV ingestion, deterministic chunking, and keyword-ranked SQL retrieval for grounded chat.
- A JSON-RPC MCP endpoint, tool discovery/execution, and configurable HTTP MCP server clients.
- A2A agent-card publication, local task execution, and configurable HTTP peer discovery/task clients.
- External OIDC/JWT authentication in production, development-only anonymous access, admin-only configuration/ingestion/remote-tool operations, audit records, API/chat rate limits, retries, and request timeouts.
- React chat and knowledge screens, OIDC sign-in when configured, Dockerfiles, Compose, and CI checks.

Retrieval is deliberately a database-backed lexical baseline, not a vector/embedding search implementation. The MCP endpoint is a bounded JSON-RPC HTTP implementation, not a full streaming/session transport. Production identity-provider registration, external peer credentials, and deployment secrets are environment-specific and must be configured by the operator.

## Run in GitHub Codespaces

The repository includes a Codespaces dev container with a Linux development
workspace and a SQL Server service. Your desktop does **not** need Docker or SQL
Server installed, and you do not need SSMS.

1. Push the repository to GitHub.
2. On GitHub, choose **Code → Codespaces → Create codespace on ...**. Codespaces
   builds the workspace and database containers. It generates a unique,
   ignored root `.env` file containing a development-only SQL password; that
   file is not committed.
   The Codespace workspace includes the .NET 8 SDK in its image.
3. In the Codespace terminal, start the API and frontend in separate terminals:

   ```bash
   dotnet run --no-launch-profile --project src/AgentForge.Api/AgentForge.Api.csproj --urls http://0.0.0.0:5151
   ```

   ```bash
   npm --prefix web run dev -- --host 0.0.0.0
   ```

   Codespaces forwards both ports; open the forwarded **AgentForge web** port.
   The API applies EF migrations and seeds starter agents when it starts.
   To launch both servers from one command, run **Tasks: Run Task** from the
   VS Code Command Palette and choose **AgentForge full stack**.
4. To enable Gemini or OpenAI chat, store provider credentials as Codespaces
   secrets, then map them into .NET user-secrets inside the Codespace:

   ```bash
   test -n "${GEMINI_API_KEY:-}" || { echo "GEMINI_API_KEY is missing from this Codespace"; exit 1; }
   dotnet user-secrets init --project src/AgentForge.Api/AgentForge.Api.csproj
   dotnet user-secrets set "Models:Gemini:ApiKey" "$GEMINI_API_KEY" --project src/AgentForge.Api/AgentForge.Api.csproj
   ```

   Add `GEMINI_API_KEY` as a Codespaces secret before creating the Codespace.
   The guard prevents saving an empty key when the Codespace has not received
   the secret.
   The default Gemini model is `gemini-3.5-flash-lite`; existing overrides in
   `Models:Gemini:Model` should use a model enabled for the key's project.
   The API also reads `GEMINI_API_KEY` and `OPENAI_API_KEY` directly when set
   in its environment, so restart the API after changing a Codespaces secret.
   Alternatively, Ollama can be configured if a reachable Ollama service is
   available. Do not paste credentials into source files or commit `.env`.

The Codespaces SQL Server data uses a named volume. It survives ordinary
container restarts/rebuilds but is not a backup; deleting the Codespace can
delete its data. Keep forwarded ports private. Development mode intentionally
does not require authentication, so do not make those ports public. Codespaces
is suitable for development and private demos, not as an always-on production
host. A real production deployment needs managed
hosting/database, TLS, a configured OIDC identity provider, secret management,
backups, and a deployment workflow.

### Database tools (SSMS is optional)

The API applies the checked-in EF Core migrations automatically. For database
inspection, the dev container recommends the Microsoft SQL Server extension
for VS Code; connect from the Codespace to `db,1433` with SQL authentication
(`sa`) and the generated `MSSQL_SA_PASSWORD` from the ignored `.env`. You can
also manage the schema with EF commands in the Codespace terminal. SSMS is a
Windows desktop tool and is not required for this workflow.

## Local development (Windows)

Prerequisites: .NET 8 SDK, Node.js 24+, a reachable SQL Server, and an OIDC
issuer/client if testing authenticated production mode. Docker Desktop is only
needed if you choose to run the local Compose stack.

1. Copy `.env.example` to `.env`, replace `MSSQL_SA_PASSWORD` with a unique strong password, and set OIDC authority/audience and at least one model provider credential. Keep `.env` local; it is ignored by Git.
2. Start SQL Server:

   ```powershell
   docker compose up -d db
   ```

3. Run the API:

   ```powershell
   dotnet run --project src\AgentForge.Api\AgentForge.Api.csproj --urls http://localhost:5151
   ```

   In Development, the API uses the local Windows SQL Server connection string in `src\AgentForge.Api\appsettings.json` by default. To use the Compose SQL Server instead, set `ConnectionStrings__AgentForge` to the same `Server=localhost,1433;Database=AgentForge;User Id=sa;Password=...;Encrypt=True;TrustServerCertificate=True` value used by Compose. EF migrations and starter agents are applied at startup.

4. In another terminal, run the web app:

   ```powershell
   cd web
   npm ci
   npm run dev
   ```

   Vite proxies `/api` to `http://localhost:5151`. OIDC is optional in local Development. Configure `VITE_OIDC_AUTHORITY`, `VITE_OIDC_CLIENT_ID`, and `VITE_OIDC_SCOPE` in a local `web/.env` to enable sign-in.

### Docker Compose

The full stack Compose profile uses production auth settings and requires both `AUTHENTICATION_AUTHORITY` and `AUTHENTICATION_AUDIENCE`, plus a configured identity client and model credentials:

```powershell
docker compose up --build
```

Open `http://localhost:8080`. Compose persists SQL data in the `agentforge-sql-data` named volume. It applies migrations on API startup for convenient local evaluation. For production, use a separate migration deployment step and a managed SQL Server; do not rely on app replicas racing to apply DDL at startup.

### Model providers

Provider selection is stored on each agent. Add the corresponding secret to environment variables or .NET user-secrets:

```powershell
dotnet user-secrets init --project src\AgentForge.Api\AgentForge.Api.csproj
dotnet user-secrets set "Models:Gemini:ApiKey" "<your-key>" --project src\AgentForge.Api\AgentForge.Api.csproj
dotnet user-secrets set "Models:OpenAI:ApiKey" "<your-key>" --project src\AgentForge.Api\AgentForge.Api.csproj
```

Ollama defaults to `http://localhost:11434` and `llama3.2`; set `Models:Ollama:BaseUrl` and install the model locally. Provider models and endpoints are configurable under `Models` and `Models:OpenAI:Endpoint`.

### MCP and A2A peers

Configure outbound MCP servers as `Mcp:Servers:<name>:Url` and remote A2A peers as `A2A:Agents:<name>:Url` using environment variables or secret configuration. Optional bearer credentials use `Mcp:Servers:<name>:BearerToken` and `A2A:Agents:<name>:BearerToken`. Use HTTPS for remote peers; plain HTTP is accepted only for loopback development endpoints. Configure outbound credentials at the deployment boundary rather than exposing credentials in agent prompts. Local MCP tools are restricted to knowledge search, enabled-agent listing, and bounded delegation.

## API overview

- `GET /` (API entry points)
- `GET /health`
- `GET|POST /agents`
- `GET /providers`
- `GET|POST /conversations`, `GET /conversations/{id}`
- `POST /chat`
- `POST /knowledge/documents`, `GET /knowledge/search?query=...`
- `POST /mcp` (`initialize`, `tools/list`, `tools/call`)
- `GET /.well-known/agent-card.json`, `POST /a2a/tasks`
- `GET /mcp/remote-tools`, `POST /mcp/servers/{serverName}/tools/{toolName}`
- `GET /a2a/agents`, `POST /a2a/agents/{agentName}/tasks`

Swagger is available at `/swagger` in Development. Ready-to-run examples are in `src/AgentForge.Api/AgentForge.Api.http`.

## Database migrations

The initial schema is in `src/AgentForge.Infrastructure/Data/Migrations`. The repository-pinned EF CLI is restored with `dotnet tool restore`.

```powershell
dotnet tool run dotnet-ef migrations list --project src\AgentForge.Infrastructure\AgentForge.Infrastructure.csproj --startup-project src\AgentForge.Api\AgentForge.Api.csproj
dotnet tool run dotnet-ef database update --project src\AgentForge.Infrastructure\AgentForge.Infrastructure.csproj --startup-project src\AgentForge.Api\AgentForge.Api.csproj
```

Create future migrations with `dotnet tool run dotnet-ef migrations add <Name> --project src\AgentForge.Infrastructure\AgentForge.Infrastructure.csproj --startup-project src\AgentForge.Api\AgentForge.Api.csproj --output-dir Data\Migrations`.

## Tests and checks

```powershell
dotnet build AgentForge.slnx
dotnet test tests\AgentForge.Tests\AgentForge.Tests.csproj
cd web
npm ci
npm run lint
npm run build
```

The EF-backed runtime requires a reachable SQL Server. Chat tests use fake repositories and model adapters; integration smoke tests do not require an external model API.

## Deployment/security notes

- Set `ASPNETCORE_ENVIRONMENT=Production`; startup fails if the JWT authority or audience is absent. Production API routes require a valid Bearer token; agent creation, document ingestion, and remote MCP tool execution require the `Admin` role.
- The frontend uses authorization-code OIDC through `react-oidc-context` when configured. Register its exact redirect URI and API audience/scope with the identity provider.
- Use a managed secrets store and least-privilege SQL account in production. Rotate provider keys and database credentials; do not put secrets in `appsettings*.json`, images, or source control.
- Configure TLS at the ingress/reverse proxy, network policies, backups, log retention, and provider budgets. The sample Compose SQL configuration trusts the container SQL certificate and is for local evaluation only.
- Current seed agent definitions are inserted only into an empty agent table. Database rows, not process memory, are the source of truth.

## CI

`.github/workflows/ci.yml` restores/builds/tests .NET, validates the migration script, runs frontend lint/build, and builds both container images without publishing them.
