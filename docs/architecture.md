# AgentForge architecture

```text
React + TypeScript
  ├─ OIDC authorization-code client (optional locally; required for production API access)
  └─ typed REST client
       │ same-origin /api proxy
       ▼
ASP.NET Core API (minimal endpoints, request validation, Problem Details,
JWT authorization, fixed-window rate limits, audit events)
       │
       ├─ Application services
       │    ├─ Agent orchestrator and durable conversation service
       │    ├─ Model provider/registry contracts
       │    ├─ Bounded tool executor (knowledge, agent listing, delegation)
       │    └─ Knowledge ingest/search contracts
       │
       ├─ Infrastructure
       │    ├─ EF Core + SQL Server migrations/repositories
       │    ├─ Gemini / OpenAI / Ollama HTTP adapters
       │    ├─ lexical chunk retrieval
       │    ├─ HTTP MCP peer client
       │    └─ HTTP A2A peer client
       │
       └─ SQL Server
            ├─ Agents
            ├─ Conversations + ordered messages
            ├─ Knowledge documents + chunks
            └─ Audit entries
```

## Trust boundaries and execution

- Model text is treated as untrusted. A chat completion may request only a named tool from a small server-side allow-list; arbitrary shell, SQL, URL, or file tools are not exposed. Tool arguments are bounded and validated. Delegation can target only another enabled, configured AgentForge agent and is capped to one tool round with at most three calls.
- Retrieved document text is passed as reference-only context. It is not parsed as authorization or executable instructions.
- Remote MCP/A2A URLs come only from operator configuration (never model/user-selected URLs), require HTTPS except loopback development, and outbound HTTP redirects are disabled for model calls. The egress environment should still use network-level destination controls.
- Provider keys are read from secret configuration and are never returned by provider metadata endpoints.
- Production API routes require bearer-token validation by the configured issuer and audience. Privileged configuration writes require the `Admin` role. Authentication is intentionally relaxed only in the Development environment.

## Current boundaries

Knowledge retrieval is SQL-backed lexical matching rather than semantic vector search. MCP supports bounded JSON-RPC HTTP calls and remote HTTP discovery, not streaming transport/session negotiation. The A2A peer client expects the AgentForge task endpoint shape; interoperability with arbitrary A2A implementations needs protocol conformance tests and authentication mapping.

Use the infrastructure interfaces to replace the baseline retrieval/transport implementations without changing the application contracts.
