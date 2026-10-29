# Imotek

Aplikasyon web immobilier pou Ayiti.

| Kouch | Teknoloji |
|-------|-----------|
| Backend | ASP.NET Core (.NET 10 LTS), REST API |
| Achitekti | Clean Architecture |
| ORM | Entity Framework Core 10 + Npgsql |
| Baz done | PostgreSQL 18 (Docker) |
| Tès | xUnit v3 (Microsoft.Testing.Platform) |
| Frontend | React 19 + TypeScript + Vite |

## Estrikti repository a

Pwojè a se yon dosye nan workspace `ai-dev` la, ki ka gen lòt pwojè tou. Chak pwojè gen pwòp depo Git pa l. Memwa workspace la (`ai-dev/MEMORY.md`) rete deyò depo sa a.

```
imotek/                              # Rasin depo Git la
├── compose.yaml                     # PostgreSQL pou devlopman
├── .env.example                     # Modèl varyab anviwònman (kopye l nan .env)
├── backend/
│   ├── Imotek.slnx
│   ├── global.json                  # SDK .NET 10 + runner tès MTP
│   ├── dotnet-tools.json            # dotnet-ef 10 (zouti lokal)
│   ├── src/
│   │   ├── Imotek.Domain/           # Antite, règ biznis (pa depann de anyen)
│   │   ├── Imotek.Application/      # Ka itilizasyon        → Domain
│   │   ├── Imotek.Infrastructure/   # EF Core, PostgreSQL   → Application, Domain
│   │   │   ├── Persistence/ImotekDbContext.cs
│   │   │   ├── Persistence/Migrations/
│   │   │   └── DependencyInjection.cs
│   │   └── Imotek.API/              # REST, health checks   → Application, Infrastructure
│   └── tests/
│       └── Imotek.Tests/            # Tès achitekti, Infrastructure, API
├── frontend/                        # React + TypeScript (Vite)
│   └── src/components/
├── .gitignore
└── README.md
```

### Règ depandans (Clean Architecture)

```
API ──► Application ──► Domain
 │           ▲
 └──► Infrastructure ──┘ (+ Domain)
```

Tès `Architecture/LayerDependencyTests` yo verifye règ sa a otomatikman.

## Prerekizit

- .NET SDK 10.0.x
- Node.js `^20.19` oswa `>=22.12` (Vite 8)
- Docker Desktop (Docker Compose v2)

## 1. Varyab anviwònman

```bash
cp .env.example .env
# Modifye .env epi mete yon modpas devlopman pou POSTGRES_PASSWORD
```

| Varyab | Wòl | Default |
|--------|-----|---------|
| `POSTGRES_DB` | Non baz done a | `imotek_dev` |
| `POSTGRES_USER` | Itilizatè PostgreSQL | `imotek` |
| `POSTGRES_PASSWORD` | Modpas (obligatwa, pa janm commit li) | — |
| `POSTGRES_PORT` | Pò sou machin lokal la | `5433` |

> Nou itilize pò **5433** paske 5432 souvan deja okipe pa yon PostgreSQL ki enstale lokalman.

`.env` eskli nan Git. Sèl `.env.example` ki commit.

## 2. Demare PostgreSQL ak Docker

```bash
docker compose up -d          # demare
docker compose ps             # estati (dwe montre "healthy")
docker compose logs postgres  # log yo
docker compose stop           # kanpe san efase done yo
```

Done yo konsève nan volume `imotek-postgres-data`.
**Pa itilize `docker compose down -v`**: opsyon `-v` la efase volume nan ak tout done yo.

## 3. Konekte API a ak baz done a

Connection string la rele `ConnectionStrings:ImotekDb`. `appsettings.Development.json` gen yon vèsyon **san modpas**. Mete connection string konplè a nan user-secrets (valè yo dwe matche ak `.env`):

```bash
cd backend/src/Imotek.API
dotnet user-secrets set "ConnectionStrings:ImotekDb" "Host=localhost;Port=5433;Database=imotek_dev;Username=imotek;Password=<POSTGRES_PASSWORD>;Timeout=5"
```

Nan lòt anviwònman (CI, pwodiksyon), itilize varyab anviwònman:

```bash
ConnectionStrings__ImotekDb="Host=...;Port=...;Database=...;Username=...;Password=..."
```

API a refize demare si connection string la pa konfigire.

## 4. Lanse backend ak frontend

**Backend**
```bash
cd backend
dotnet restore
dotnet run --project src/Imotek.API --launch-profile http
```

**Frontend** (nan yon lòt terminal)
```bash
cd frontend
npm install
npm run dev
```

| Sèvis | URL |
|-------|-----|
| Frontend | http://localhost:5173 |
| API | http://localhost:5056 |
| OpenAPI (Development) | http://localhost:5056/openapi/v1.json |

Vite voye demann `/api/*` bay API a, kidonk pa bezwen CORS pandan devlopman.

## 5. Endpoint verifikasyon

| Endpoint | Sa li verifye | Repons |
|----------|---------------|--------|
| `GET /health` | API a ap kouri (pa teste DB a) | `200` `{"status":"Healthy","checks":[]}` |
| `GET /health/db` | Koneksyon reyèl ak PostgreSQL | `200` si DB a disponib, `503` si li pa disponib |

Repons yo genyen sèlman non ak estati check yo, pa janm connection string, modpas oswa mesaj eksepsyon.

```bash
curl http://localhost:5056/health
curl http://localhost:5056/health/db
```

## 6. Migrasyon database

`dotnet-ef` 10 enstale kòm **zouti lokal** (`backend/dotnet-tools.json`), kidonk li pa depann de vèsyon global ki sou machin nan.

```bash
cd backend
dotnet tool restore

# Kreye yon migrasyon
dotnet ef migrations add <NonMigrasyon> -p src/Imotek.Infrastructure -s src/Imotek.API -o Persistence/Migrations

# Aplike migrasyon yo sou baz done devlopman an
dotnet ef database update -p src/Imotek.Infrastructure -s src/Imotek.API

# Lis migrasyon yo
dotnet ef migrations list -p src/Imotek.Infrastructure -s src/Imotek.API
```

Premye migrasyon an (`InitialBaseline`) **vid**: Domain lan poko gen antite. Li sèlman kreye tab istorik `__EFMigrationsHistory`. Konfigirasyon antite yo pral nan `Imotek.Infrastructure` kòm `IEntityTypeConfiguration<T>`. `ImotekDbContext` chaje yo otomatikman.

## 7. Tès

```bash
cd backend
dotnet test --solution Imotek.slnx
```

Tès yo pa bezwen yon PostgreSQL reyèl:
- **Architecture**: chak kouch pa referans kouch ki deyò l (ni EF Core/Npgsql/ASP.NET nan Domain ak Application).
- **Infrastructure**: anrejistreman `ImotekDbContext` (provider Npgsql), health check DB a, erè lè connection string la manke.
- **Api**: `/health` ak `/health/db` (503 lè DB a pa disponib, san fwit enfòmasyon sansib).

> `global.json` aktive Microsoft.Testing.Platform pou `dotnet test` (obligatwa pou xUnit v3 sou .NET 10 SDK).

## 8. Build

```bash
cd backend  && dotnet build -c Release
cd frontend && npm run build
cd frontend && npm run lint
```
