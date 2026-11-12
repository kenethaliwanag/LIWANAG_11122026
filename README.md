# CSV Processing API

A secure ASP.NET Core Web API that accepts uploaded CSV files, calculates aggregates over their
numeric columns, keeps a history of every processing attempt, and exposes reporting endpoints.

---

## Features

- **CSV upload** – accepts `multipart/form-data` files, with a hard limit of 5 MB and 100 000 data rows.
- **Content-driven validation** – extension, strict UTF-8 encoding, binary content, header integrity and row
  structure are all validated from the actual bytes, never from the declared content type alone.
- **Schema-agnostic aggregation** – every column is inspected; a column is treated as numeric only when it holds
  at least one non-empty value and *all* of its non-empty values parse as numbers.
- **Count, Sum, Average, Minimum, Maximum** – calculated with `decimal` and invariant culture.
- **Processing history** – every attempt, successful or failed, is persisted with a sanitized error message.
- **Reporting** – per-run aggregates, paginated history with status and date filters, and a cross-run summary.
- **API key authentication** – a custom `X-Api-Key` handler with constant-time key comparison.
- **UTC timestamps** – stored as SQLite `INTEGER` ticks so ordering and date filtering are exact.
- **Docker ready** – multi-stage image, non-root user, persistent SQLite volume, migrations applied on startup.

## Technology stack

| Component | Version |
|---|---|
| .NET SDK | 10.0.300 (LTS), pinned in `global.json` |
| ASP.NET Core runtime | 10.0.8 |
| CsvHelper | 34.0.0 |
| Swashbuckle.AspNetCore | 11.0.0 (OpenAPI 3) |
| Entity Framework Core | 10.0.15 (SQLite provider) |
| `dotnet-ef` | 10.0.15 (local tool manifest) |
| xUnit | 2.9.3 with `Microsoft.NET.Test.Sdk` 18.11.1 |

## Prerequisites

- .NET 10 SDK (10.0.300 or later in the 10.0.x feature band)
- Optional: Docker Desktop, for the container workflow
- Optional: the .NET CLI `dotnet-ef` tool (installed locally through the tool manifest)

## Project structure

```
csv-processing-api/
├── .config/dotnet-tools.json        local tool manifest (dotnet-ef 10.0.15)
├── .dockerignore                    build context exclusions
├── .editorconfig
├── .env.example                     template for the API key used by Docker Compose
├── .gitignore
├── Directory.Build.props            shared compiler settings
├── Dockerfile                       multi-stage build, non-root runtime
├── docker-compose.yml               API service with a named SQLite volume
├── global.json                      pins the .NET SDK
├── CsvProcessing.sln
├── src/CsvProcessing.Api/
│   ├── Contracts/                   request and response DTOs
│   ├── Controllers/                 Uploads, History, Reports, Health
│   ├── Data/                        AppDbContext, UTC ticks converter, migrations
│   ├── Domain/                      ProcessingRun, ColumnAggregate, ProcessingStatus
│   ├── Infrastructure/              file validation, API key authentication
│   ├── Middleware/                  sanitized exception handling
│   ├── Services/                    CSV parsing, aggregation, processing, reporting
│   └── Program.cs                   composition root
└── tests/CsvProcessing.Tests/
    ├── Unit/                        parser, calculator, validator, key comparer
    ├── Integration/                 auth, uploads, history, reports, timestamps, Swagger
    ├── Data/                        EF Core model and converter tests
    └── TestApiFactory.cs            boots the API against a temporary SQLite file
```

## Installation and local setup

```bash
git clone <your-repository-url>
cd csv-processing-api

dotnet tool restore          # installs dotnet-ef 10.0.15 from the local manifest
dotnet restore
dotnet build
```

### User Secrets configuration

The API refuses to start without an API key. For local development, store it in user secrets so it never
enters the repository:

```bash
cd src/CsvProcessing.Api
dotnet user-secrets init                       # only needed once
dotnet user-secrets set "ApiKey:Key" "<your-key>"
```

User secrets live outside the project in your user profile, are ignored by git, and are only loaded in the
`Development` environment. For any other environment use the environment variable `ApiKey__Key`.

## Database migrations

Migrations are managed with EF Core. The tool manifest pins the matching version, so no global install is needed:

```bash
# list applied migrations
dotnet ef migrations list --project src/CsvProcessing.Api --startup-project src/CsvProcessing.Api

# create a new migration after changing the model
dotnet ef migrations add <MigrationName> --project src/CsvProcessing.Api --startup-project src/CsvProcessing.Api

# apply migrations to the local database
dotnet ef database update --project src/CsvProcessing.Api --startup-project src/CsvProcessing.Api
```

The application also applies any pending migrations automatically at startup
(`Database:ApplyMigrationsOnStartup`, default `true`), which is what the container relies on.

## Running the application

```bash
dotnet run --project src/CsvProcessing.Api
```

`dotnet run` uses the `http` launch profile from `Properties/launchSettings.json`, so the API is served on
<http://localhost:5296> with the `Development` environment. The `https` profile additionally serves
<https://localhost:7067>. Swagger UI is available at `/swagger` and the raw OpenAPI document at
`/swagger/v1/swagger.json`.

```bash
curl http://localhost:5296/health
# {"status":"Healthy"}
```

## API key authentication

Every endpoint except `GET /health` requires the `X-Api-Key` header.

```bash
curl -H "X-Api-Key: <your-key>" http://localhost:5296/api/history
```

- Missing, empty or invalid keys are rejected with **401 Unauthorized** and `WWW-Authenticate: ApiKey`.
- Keys are compared in constant time against a SHA-256 digest, so neither the key length nor the matching
  position is observable through response timing.
- Several keys can be configured at once by separating them with commas. This allows rotation without downtime.
- The key is never written to the repository, to the logs, or to the container image.

## API endpoints

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/uploads` | Uploads a CSV file, validates it, calculates the aggregates and records the run. |
| `GET` | `/api/history` | Paginated processing history, newest first. Filters: `status`, `from`, `to`. |
| `GET` | `/api/history/{runId}` | One run in detail, including its calculated aggregates. |
| `GET` | `/api/reports/aggregates/{runId}` | The aggregate report of a single run. |
| `GET` | `/api/reports/summary` | Totals across all runs: counts, rows processed, first and last upload. |
| `GET` | `/health` | Liveness and database probe. Public, no API key required. |

`GET /api/history` accepts `page` (default `1`) and `pageSize` (default `20`, maximum `100`).

## CSV format and aggregation rules

A CSV file must be UTF-8 encoded, use the `.csv` extension, contain a header row and at least one data row,
and every data row must have exactly as many fields as the header.

```
name,amount,ratio
alpha,10,0.5
beta,20,1.5
gamma,,2.5
```

Aggregation rules:

| Rule | Behaviour |
|---|---|
| Numeric detection | A column is numeric only when it has **at least one** non-empty value and **every** non-empty value parses as a number. One non-numeric value disqualifies the whole column. |
| Empty cells | Ignored completely: they are not counted and do not affect the sum, the average, the minimum or the maximum. |
| Number format | `decimal`, parsed with invariant culture. A leading sign and a decimal point are allowed. |
| Thousands separators / exponents | `1,234` and `1e5` are **not** numbers, so such a column is skipped. |
| Non-numeric columns | Produce no aggregate at all. |
| Overflow | A column whose total exceeds the `decimal` range fails the run with a clear message instead of crashing. |
| Header rules | Empty and duplicate headers are rejected. |

For the sample above the API returns:

| Column | Count | Sum | Average | Minimum | Maximum |
|---|---|---|---|---|---|
| `amount` | 2 | 30 | 15 | 10 | 20 |
| `ratio` | 3 | 4.5 | 1.5 | 0.5 | 2.5 |

`name` produces no aggregate because it is not numeric. `amount` has a count of 2 because the empty cell is
ignored.

## Sample requests and responses

### Upload a CSV

`sample.csv` is included in the repository root, so this command works as written from that directory:

```bash
curl -X POST http://localhost:5296/api/uploads \
  -H "X-Api-Key: <your-key>" \
  -F "file=@sample.csv"
```

```json
{
  "runId": 1,
  "fileName": "sample.csv",
  "status": "Completed",
  "rowCount": 3,
  "columnCount": 3,
  "numericColumnCount": 2,
  "uploadedAt": "2026-11-11T14:19:23.1924565Z",
  "completedAt": "2026-11-11T14:19:23.2009798Z",
  "aggregates": [
    {
      "columnName": "amount",
      "columnIndex": 1,
      "count": 2,
      "sum": 30,
      "average": 15,
      "minimum": 10,
      "maximum": 20
    },
    {
      "columnName": "ratio",
      "columnIndex": 2,
      "count": 3,
      "sum": 4.5,
      "average": 1.5,
      "minimum": 0.5,
      "maximum": 2.5
    }
  ]
}
```

### Read the history

```bash
curl -H "X-Api-Key: <your-key>" "http://localhost:5296/api/history?page=1&pageSize=20&status=Failed"
```

```json
{
  "items": [
    {
      "id": 3,
      "fileName": "broken.csv",
      "fileSizeBytes": 28,
      "status": "Failed",
      "rowCount": 0,
      "columnCount": 0,
      "numericColumnCount": 0,
      "uploadedAt": "2026-11-11T14:19:23.3015221Z",
      "completedAt": "2026-11-11T14:19:23.3041022Z",
      "errorMessage": "Malformed CSV data at record 2 (line 2)."
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

### Read the summary

```bash
curl -H "X-Api-Key: <your-key>" http://localhost:5296/api/reports/summary
```

```json
{
  "totalRuns": 6,
  "completedRuns": 2,
  "failedRuns": 4,
  "totalRowsProcessed": 4,
  "totalBytesProcessed": 167,
  "earliestUploadAt": "2026-11-11T14:19:21.0561717Z",
  "latestUploadAt": "2026-11-11T14:19:23.6146274Z"
}
```

## Error handling and HTTP status codes

| Status | When it is returned |
|---|---|
| `200 OK` | Successful read, or a successful upload. |
| `201 Created` | The file was processed; the response contains the run and its aggregates. |
| `400 Bad Request` | No file part was sent, or the file does not use the `.csv` extension. |
| `401 Unauthorized` | The API key is missing, empty or invalid. `WWW-Authenticate: ApiKey` is returned. |
| `404 Not Found` | The requested run does not exist. |
| `413 Payload Too Large` | The file exceeds 5 MB. |
| `422 Unprocessable Entity` | The file could not be processed: malformed CSV, inconsistent field counts, invalid headers, more rows than allowed, invalid UTF-8, binary content, or numeric overflow. |
| `500 Internal Server Error` | An unexpected server fault. The response contains a generic message only. |
| `503 Service Unavailable` | The health check could not reach the database. |

Error messages never contain uploaded cell values, and the same sanitized message is stored in the run history.
The `ErrorMessage` column on a failed run therefore only ever describes *what* was wrong, never the data.

## Automated testing

```bash
dotnet test
```

The suite contains unit tests for the parser, the aggregation rules, the file validator and the key comparer,
plus integration tests that boot the real API against a temporary SQLite file:

| Area | Covered |
|---|---|
| Authentication | missing key, empty key, invalid key, valid key, protected GETs, public health endpoint |
| Uploads | valid CSV, malformed CSV, wrong extension, oversized file, missing file part, non-numeric columns |
| Aggregation | numeric detection, mixed columns, empty cells, invariant culture, overflow |
| History and reports | failed runs persisted and listed, status filter, run detail, aggregates report, summary |
| Persistence | UTC round trip, `INTEGER` column type, chronological ordering, date range filtering |
| Documentation | the OpenAPI document generates and documents every endpoint |

## Docker

### Build and run

```bash
cp .env.example .env      # then set API_KEY inside .env
docker compose up --build
```

The API is then available on <http://localhost:8080>:

```bash
curl http://localhost:8080/health
curl -H "X-Api-Key: <your-key>" http://localhost:8080/api/history
```

To build and run the image directly:

```bash
docker build -t csv-processing-api:latest .
docker run -d --name csv-processing-api \
  -p 8080:8080 \
  -e ApiKey__Key="<your-key>" \
  -v csv-data:/app/data \
  csv-processing-api:latest
```

The image is multi-stage: the .NET 10 SDK builds and publishes the application, and the final stage contains
only the ASP.NET Core runtime. The container runs as the non-root `app` user and listens on port 8080.

### HTTPS in front of the container

The container serves **plain HTTP** internally, which is fine for local development or a trusted private
network. For any internet-facing deployment, put a TLS-terminating reverse proxy (nginx, Caddy, an ingress
controller, a cloud load balancer) in front of port 8080 and terminate HTTPS there.

> **Never send an API key over unencrypted public HTTP.** The key is a bearer credential: anyone who can read
> it in transit can call the API. Over a private network, encrypt the traffic or use a tunnel.

### SQLite persistence

The database file lives at `/app/data/csvprocessing.db` inside the container, and `/app/data` is a named
Docker volume (`csv-data`). The data therefore survives `docker compose down`, container restarts and image
rebuilds.

```bash
docker volume ls | grep csv-data       # the volume exists
docker compose exec api ls -l /app/data # the database file is there
docker compose down                     # the volume and its data are kept
docker compose up                       # the history is still present
```

To discard the data completely:

```bash
docker compose down -v                  # removes the volume as well
```

Migrations are applied automatically when the container starts, so a fresh container immediately has the
correct schema.

## GitHub submission

The repository must be named using the **`LAST_NAME_MMDDYYYY`** convention, where `MMDDYYYY` is the submission date
(for example, a submission made on 12 November 2026 would be named `LASTNAME_11122026`).

```bash
# create the repository on GitHub with that name, then:
git remote add origin https://github.com/<your-user>/LAST_NAME_MMDDYYYY.git
git push -u origin main
```

Keep the commit history intact – do not squash, rebase or force-push. No secret, API key or local database
file may be committed; `.gitignore` and `.dockerignore` already exclude them.
