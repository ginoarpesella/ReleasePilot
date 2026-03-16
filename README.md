# ReleasePilot

**Application & Environment Lifecycle Manager** — A .NET REST API that manages the lifecycle of application version promotions through deployment environments (dev → staging → production).

---

## Table of Contents

- [Architecture & Design Decisions](#architecture--design-decisions)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [API Reference & Example Requests](#api-reference--example-requests)
- [Running Tests](#running-tests)
- [Postman Collection](#postman-collection)
- [Makefile](#makefile)
- [Design Deep Dive](#design-deep-dive)
- [Stretch Goal: AI Release Notes Agent](#stretch-goal-ai-release-notes-agent)
- [What I Would Do Next](#what-i-would-do-next)

---

## Architecture & Design Decisions

### Domain-Driven Design (DDD)

The `Promotion` aggregate is the central concept. It protects all its own invariants — no business logic leaks into command handlers or the API layer. The aggregate:

- Enforces the state machine transitions (Requested → Approved → InProgress → Completed/RolledBack, or Requested → Cancelled)
- Validates environment promotion order (dev → staging → production, no skipping)
- Ensures immutability of terminal states (Completed, Cancelled)
- Records state history as value objects
- Raises domain events for every transition

**Ubiquitous Language** is used throughout: `Promotion`, `DeploymentEnvironment`, `StateTransition`, `Approve`, `Rollback`, etc.

### CQRS (Command Query Responsibility Segregation)

Write and read sides are completely separated:

- **Commands** (`RequestPromotion`, `ApprovePromotion`, etc.) go through dedicated command handlers via MediatR. Each handler loads the aggregate, invokes the domain method, persists changes, and publishes events. Handlers contain **zero business logic** — all invariants live in the domain model.
- **Queries** (`GetPromotionById`, `GetEnvironmentStatus`, `ListPromotionsByApplication`) use a separate `IPromotionReadRepository` with read models designed for the consumer (dashboard summaries, paginated lists, detailed views with state history). Read models are **not** the aggregate — they're flat DTOs optimized for display.

### Event-Driven Architecture with Outbox Pattern

Every state transition emits a domain event. Events flow through the **Outbox pattern**:

1. **Write side**: Domain events are serialized and saved to the `OutboxMessages` table in the **same database transaction** as the aggregate changes. This guarantees consistency — if the aggregate save succeeds, the events are guaranteed to be published.
2. **Outbox Processor** (background service): Polls unprocessed outbox messages and publishes them to **RabbitMQ** via a fanout exchange.
3. **Audit Log Consumer** (background service): Consumes events from RabbitMQ and persists them as audit log entries (event type, promotion ID, timestamp, acting user, full payload).

The API returns its response **before** the consumer finishes processing — true fire-and-forget async processing.

**Why Outbox?** Direct publication to RabbitMQ from the command handler risks data inconsistency: the aggregate could be saved but the message lost (or vice versa). The Outbox pattern provides at-least-once delivery guarantees without distributed transactions.

### Ports & Adapters (Hexagonal Architecture)

External system interfaces are defined as **ports** in the Domain layer (`IDeploymentPort`, `IIssueTrackerPort`, `INotificationPort`). This keeps the domain pure — it depends on abstractions, not infrastructure.

**Adapters** (stub implementations) live in the Infrastructure layer:
- `StubDeploymentAdapter` — logs deployment initiation with simulated latency
- `StubIssueTrackerAdapter` — returns realistic fake work items (with breaking changes, vague descriptions)
- `StubNotificationAdapter` — logs notification sends

The ports are in the Domain layer (not Application) because the domain aggregate's behavior may need to reference these contracts, and they represent domain concepts (deploying, tracking issues, notifying).

### Resilience with Polly

RabbitMQ connections in both the `OutboxProcessor` and `AuditLogConsumer` use **Polly** resilience pipelines with exponential backoff retry. The retry policy has no upper attempt limit — the background services will keep retrying with increasing delays (capped at 2 minutes) until a connection succeeds. This ensures the application starts gracefully even if RabbitMQ is slow to become available.

### Structured Logging with Serilog

The application uses **Serilog** for structured logging with two sinks:
- **Console** — standard output for development and container environments
- **File** — rolling daily log files written to the `logs/` directory relative to the application base directory, with a 30-day retention policy

Serilog replaces the default ASP.NET Core logging pipeline and enriches log entries with context properties via `Enrich.FromLogContext()`.

### Error Handling

Domain rule violations produce `DomainException` with a structured error code. The `DomainExceptionMiddleware` catches these and returns HTTP 422 (Unprocessable Entity) with a clear JSON error body — never a 500.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or later)
- [Docker](https://www.docker.com/products/docker-desktop) and Docker Compose
- (Optional) [dotnet-ef tool](https://docs.microsoft.com/en-us/ef/core/cli/dotnet) for migrations

---

## Getting Started

### 1. Start Infrastructure

```bash
cd ReleasePilot
docker compose up -d
```

This starts:
- **PostgreSQL** on port `5432` (user: `releasepilot`, password: `releasepilot`, database: `releasepilot`)
- **RabbitMQ** on port `5672` (management UI on `15672`, user: `guest`, password: `guest`)

### 2. Run the API

**Option A: Run locally**

```bash
dotnet run --project src/ReleasePilot.Api
```

The API starts on:
- **HTTP**: `http://localhost:5180`
- **HTTPS**: `https://localhost:7067`

To run with HTTPS explicitly:

```bash
dotnet run --project src/ReleasePilot.Api --launch-profile https
```

**Option B: Run everything with Docker (including the API)**

```bash
docker compose up -d --build
```

This starts PostgreSQL, RabbitMQ, and the API container. The API is exposed on `http://localhost:5180`.

Database migrations run automatically on startup.

### 3. Swagger UI

In development mode, Swagger UI is available at:

- http://localhost:5180/swagger
- https://localhost:7067/swagger

Use it to explore and test all API endpoints interactively.

### 4. Verify Health

```bash
curl http://localhost:5180/health
```

Expected: `{"status":"healthy","timestamp":"..."}`

---

## API Reference & Example Requests

### Commands (Write Side)

#### 1. Request a Promotion

```bash
curl -X POST http://localhost:5180/api/promotions \
  -H "Content-Type: application/json" \
  -d '{
    "applicationName": "PaymentService",
    "version": "2.1.0",
    "sourceEnvironment": 0,
    "targetEnvironment": 1,
    "requestedBy": "dev@example.com",
    "workItemReferences": ["WI-101", "WI-102", "WI-103"]
  }'
```

> Environments: `0` = Dev, `1` = Staging, `2` = Production

**Response** (201 Created):
```json
{ "id": "a1b2c3d4-..." }
```

#### 2. Approve a Promotion

```bash
curl -X POST http://localhost:5180/api/promotions/{id}/approve \
  -H "Content-Type: application/json" \
  -d '{
    "approvedBy": "lead@example.com",
    "isApprover": true
  }'
```

> This also triggers the **AI Release Notes Agent** if work items are referenced.

#### 3. Start Deployment

```bash
curl -X POST http://localhost:5180/api/promotions/{id}/deploy \
  -H "Content-Type: application/json" \
  -d '{
    "startedBy": "deploy-bot@example.com"
  }'
```

#### 4. Complete a Promotion

```bash
curl -X POST http://localhost:5180/api/promotions/{id}/complete
```

#### 5. Rollback a Promotion

```bash
curl -X POST http://localhost:5180/api/promotions/{id}/rollback \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Critical performance regression detected in staging",
    "rolledBackBy": "oncall@example.com"
  }'
```

#### 6. Cancel a Promotion

```bash
curl -X POST http://localhost:5180/api/promotions/{id}/cancel \
  -H "Content-Type: application/json" \
  -d '{
    "cancelledBy": "dev@example.com"
  }'
```

### Queries (Read Side)

#### Get Promotion by ID (with state history)

```bash
curl http://localhost:5180/api/promotions/{id}
```

**Response**:
```json
{
  "id": "...",
  "applicationName": "PaymentService",
  "version": "2.1.0",
  "sourceEnvironment": "Dev",
  "targetEnvironment": "Staging",
  "status": "Approved",
  "requestedBy": "dev@example.com",
  "createdAt": "2026-03-16T12:00:00Z",
  "completedAt": null,
  "rollbackReason": null,
  "workItemReferences": ["WI-101", "WI-102"],
  "stateHistory": [
    { "fromStatus": "Requested", "toStatus": "Requested", "actingUser": "dev@example.com", "transitionedAt": "...", "reason": null },
    { "fromStatus": "Requested", "toStatus": "Approved", "actingUser": "lead@example.com", "transitionedAt": "...", "reason": null }
  ]
}
```

#### Get Environment Status (Dashboard)

```bash
curl http://localhost:5180/api/promotions/application/PaymentService/status
```

**Response**:
```json
{
  "applicationName": "PaymentService",
  "environments": [
    { "environment": "Dev", "activePromotionId": null, "currentVersion": null, "status": null, "lastUpdated": null },
    { "environment": "Staging", "activePromotionId": "...", "currentVersion": "2.1.0", "status": "InProgress", "lastUpdated": "..." },
    { "environment": "Production", "activePromotionId": null, "currentVersion": null, "status": null, "lastUpdated": null }
  ]
}
```

#### List Promotions by Application (Paginated)

```bash
curl "http://localhost:5180/api/promotions/application/PaymentService?page=1&pageSize=10"
```

### Error Responses

Domain violations return HTTP 422:
```json
{
  "type": "DomainError",
  "code": "INVALID_TRANSITION",
  "message": "Cannot approve a promotion in 'Approved' state. Must be in 'Requested' state."
}
```

---

## Running Tests

```bash
dotnet test
```

38 unit tests cover the entire Promotion state machine:
- Valid and invalid state transitions for every command
- Environment validation (valid paths, skipping environments)
- Approver role enforcement
- Environment lock enforcement
- Immutability of terminal states
- Domain event emission
- State history tracking
- Required field validation

---

## Postman Collection

A Postman collection is included at `ReleasePilot.postman_collection.json`. It contains 17 sequential requests that exercise every API endpoint:

1. Health check
2. Request a promotion (Dev → Staging)
3. Get promotion by ID
4. List promotions by application
5. Get environment status
6. Approve promotion
7. Attempt duplicate approval (expects 422)
8. Start deployment
9. Complete promotion
10. Verify final Completed state
11–14. Full rollback flow (request → approve → deploy → rollback)
15–16. Cancel flow (request → cancel)
17. Verify immutability of cancelled state (expects 422)

**To use:** Import the JSON file into Postman and run the collection using the Collection Runner. Each request stores the `promotionId` in a collection variable so subsequent requests chain correctly.

---

## Makefile

A `Makefile` is provided for common tasks:

| Command | Description |
|---------|-------------|
| `make build` | Restore and build the solution |
| `make run` | Run the API locally (HTTP) |
| `make run-https` | Run the API locally (HTTPS) |
| `make test` | Run all unit tests |
| `make clean` | Clean build artifacts |
| `make docker-up` | Start infrastructure (PostgreSQL + RabbitMQ) |
| `make docker-run` | Build and start everything including the API container |
| `make docker-down` | Stop all containers |
| `make docker-clean` | Stop containers and remove volumes |
| `make docker-logs` | Tail API container logs |
| `make health` | Call the health endpoint |

---

## Design Deep Dive

### Why the Aggregate Protects Its Own Invariants

The `Promotion` aggregate is the sole authority on what transitions are valid. Command handlers are thin orchestrators: they load the aggregate, call a domain method, and save. This means:

- **You can't bypass rules** — even if you call the domain from a different handler or a test, the invariants hold.
- **Testing is trivial** — the 38 unit tests exercise the aggregate directly without needing mocks, databases, or HTTP.
- **The code reads like requirements** — `promotion.Approve(...)` either succeeds or throws a `DomainException` with a clear code.

### Why Outbox Pattern Over Direct Publishing

| Approach | Consistency | Complexity |
|----------|-------------|------------|
| Direct publish | Risk: DB saves but message lost | Simple |
| Distributed transaction | Strong | Very complex |
| **Outbox pattern** | **At-least-once delivery** | **Moderate** |

The outbox is written in the same `SaveChangesAsync` call as the aggregate. A background processor publishes to RabbitMQ. If the processor crashes, messages are retried on next poll.

### Why Separate Read/Write Repositories

The write repository (`IPromotionRepository`) deals with the aggregate — loading, saving, checking domain invariants like "is there an InProgress promotion for this environment?"

The read repository (`IPromotionReadRepository`) returns flattened read models — `PromotionDetailReadModel` includes state history as a flat list, `EnvironmentStatusReadModel` is a dashboard summary. These are designed for the API consumer, not for domain operations.

### Environment Validation Strategy

The promotion order (dev → staging → production) is enforced via the `DeploymentEnvironmentExtensions.IsValidPromotion` method. Only adjacent promotions are allowed: Dev→Staging and Staging→Production. This is validated at aggregate creation time, preventing invalid promotions from ever existing.

---

## Stretch Goal: AI Release Notes Agent

### Architecture

The agent is wired into the domain event lifecycle: when `PromotionApproved` fires, the `ReleaseNotesAgentHandler` (MediatR notification handler) triggers the agent.

### Tool-Calling Loop

The agent implements a proper **tool-calling loop**, not a single prompt. Using Microsoft Semantic Kernel:

1. **Agent receives** the promotion context (app, version, work item references)
2. **Agent calls** `GetWorkItems` tool → retrieves work item details via `IIssueTrackerPort`
3. **Agent reasons** over descriptions → for vague ones, calls `AskClarification` tool
4. **Agent scans** for breaking changes → calls `FlagBreakingChange` tool with reasons
5. **Agent produces** structured output → calls `SubmitReleaseNotes` tool with JSON draft

### Tool Definitions

| Tool | Contract | Purpose |
|------|----------|---------|
| `GetWorkItems(references)` | Input: comma-separated IDs → Output: formatted item list | Retrieve work item details |
| `AskClarification(workItemId, question)` | Input: ID + question → Output: response | Request more context for vague items |
| `FlagBreakingChange(workItemId, reason)` | Input: ID + reason → Output: confirmation | Mark breaking changes |
| `SubmitReleaseNotes(draftJson)` | Input: JSON draft → Output: success/failure | Submit final structured output |

### Mocked Backend

When no OpenAI API key is configured, the agent uses a **mocked LLM backend** that simulates the same tool-calling loop behavior — it calls the same tools in the same order, demonstrating the agent architecture without requiring an LLM.

To use a real LLM, add to `appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4"
  }
}
```

---

## What I Would Do Next

Given more time, these are the improvements I'd make:

1. **Dedicated read database** — In a production CQRS system, the read side would project into a separate materialized view or read-optimized store, updated by event consumers.
2. **API authentication/authorization** — Implement proper JWT-based auth with role claims instead of the `isApprover` flag in the request body.
3. **Correlation IDs** — Add correlation ID middleware for distributed tracing across services.
