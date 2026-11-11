# AgentForge web

React, TypeScript, and Vite frontend for the AgentForge API.

## Run locally

Start the API from the repository root:

```powershell
dotnet run --project src\AgentForge.Api\AgentForge.Api.csproj --urls http://localhost:5151
```

In another terminal:

```powershell
cd web
npm install
npm run dev
```

Open the URL printed by Vite. Its development proxy forwards `/api` requests to
the API at `http://localhost:5151`. To use another API base URL, set
`VITE_API_BASE_URL` in a local `.env` file; see `.env.example`.

## Checks

```powershell
npm run build
npm run lint
```
