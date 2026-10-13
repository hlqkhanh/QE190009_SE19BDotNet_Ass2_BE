# TaskTrack Backend — PRN232 Assignment 2

.NET 8 API → Service → Repository with PostgreSQL database-first mappings. All Ass1 GET/search endpoints remain public. JWT is required for resource writes; every Accounts endpoint requires Admin.

## Configuration

Set environment variables in your shell or hosting environment; `.env` is an example, not automatically loaded by .NET:

- DATABASE_URL: PostgreSQL connection string or URL.
- JWT_SECRET: random secret of at least 32 UTF-8 bytes; never commit it.
- Jwt__Issuer / Jwt__Audience: optional; defaults TaskTrack.Ass2 / TaskTrack.Frontend.
- Cors__AllowedOrigins__0: exact frontend origin.
- ASPNETCORE_ENVIRONMENT: Production on Render.

## Database and Admin

On a fresh database only, run database/TaskManagementDB_Postgres.sql. On an existing Ass1 database, run only database/Assignment2_upgrade.sql. Upgrade is transactional and repeatable, preserves existing data, adds Accounts/RefreshToken and nullable Task/Project audit IDs. The Ass1 schema file contains DROP statements.

From this repository root:

```powershell
dotnet build QE190009_SE19BDotNet_Ass1_BE.sln
dotnet run --project TaskTrack.API -- --seed-admin
dotnet run --project TaskTrack.API --urls http://localhost:5001
```

Seed prompts for name/email and a hidden password, hashes with PBKDF2-SHA256/600,000 iterations and inserts Admin directly. It never promotes or modifies an existing email. Transient SEED_ADMIN_NAME/EMAIL/PASSWORD variables support automation; clear them afterward. Register Staff through the deployed API, not through a seed.

## Endpoints

- POST /api/auth/register: Staff only; 201, duplicate email 409.
- POST /api/auth/login: session/access/refresh token; invalid credentials 401.
- POST /api/auth/refresh: rotate refresh token; absolute seven-day expiry, replay revokes the family.
- POST /api/auth/logout: invalidate all account sessions using a valid refresh token.
- GET /api/auth/me; PUT /api/auth/profile; PUT /api/auth/password: own account, authenticated.
- GET /api/accounts and /api/accounts/{id}; PUT /api/accounts/{id}; DELETE /api/accounts/{id}: Admin only.
- POST/PUT/DELETE /api/departments, /api/projects, /api/tasks, /api/tags: Staff or Admin.

Swagger is public, including production, and supports Bearer tokens. JWT lasts 24 hours (capped by session expiry), contains AccountID/Email/Role/FullName/SessionVersion/exp, validates account existence/version on every authenticated request. Password/role changes and logout invalidate old sessions. Accounts with any created task, including archived tasks, cannot be deleted. The final Admin cannot be removed/demoted. Audit IDs are server owned; passwords/token hashes never appear in responses.

## Tests and deployment

```powershell
$env:TEST_DATABASE_URL = 'Host=localhost;Port=5434;Database=tasktrack_tests;Username=taskadmin;Password=local-ass2-only'
dotnet test QE190009_SE19BDotNet_Ass1_BE.sln
docker build -t tasktrack-ass2-api .
```

Tests require a dedicated PostgreSQL database with `test` in its name and isolate each case in a temporary schema. CI provisions PostgreSQL automatically. Render Docker context is this repository root; render.yaml provisions a separate Ass2 service/database, port 10000. Copy the Ass1 database to the new empty database before upgrade and seed; never overwrite Ass1. Set CORS to the Vercel production origin. See TaskTrack.API/Assignment2.http for manual API verification.