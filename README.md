# Imotek

Aplikasyon web immobilier pou Ayiti.

| Kouch | Teknoloji |
|-------|-----------|
| Backend | ASP.NET Core (.NET 10 LTS), REST API (controllers) |
| Achitekti | Clean Architecture |
| ORM | Entity Framework Core 10 + Npgsql |
| Baz done | PostgreSQL 18 (Docker) |
| Otantifikasyon | ASP.NET Core Identity + bearer token, wòl `Admin` / `Agent` / `Client` |
| Tès | xUnit v3 (Microsoft.Testing.Platform), Testcontainers PostgreSQL |
| Frontend | React 19 + TypeScript + Vite, react-router 7 |

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
│   │   ├── Imotek.Domain/           # Property, enum yo, règ validasyon (pa depann de anyen)
│   │   ├── Imotek.Application/      # PropertyService (otorizasyon), DTO, filtè, kontra   → Domain
│   │   ├── Imotek.Infrastructure/   # EF Core, Identity, PropertyRepository, migrasyon     → Application, Domain
│   │   └── Imotek.API/              # Controllers, bearer auth, health checks              → Application, Infrastructure
│   └── tests/
│       └── Imotek.Tests/            # Domain, Application, Architecture, Infrastructure, Api
├── frontend/
│   └── src/
│       ├── api/                     # Kliyan fetch, tip, tradiksyon erè
│       ├── auth/                    # AuthProvider, useAuth, RequireRole
│       ├── components/              # Header, SearchForm, PropertyCard, …
│       └── pages/                   # Dakèy, lis, detay, koneksyon, espas ajan, fòm
├── .gitignore
└── README.md
```

### Règ depandans (Clean Architecture)

```
API ──► Application ──► Domain
 │           ▲
 └──► Infrastructure ──┘ (+ Domain)
```

- **Domain:** genyen règ pwopriyete yo (`Property.Validate`), epi li pa konnen EF Core ni ASP.NET.
- **Application:** genyen tout règ otorizasyon ak vizibilite yo (`PropertyService`). Filtè rechèch yo ekri ak LINQ pi, kidonk menm kòd la mache ni ak PostgreSQL ni nan tès inite.
- **Controllers:** yo sèlman tradui `Result` yo an kòd HTTP (`ApiControllerBase.ToProblem`).
- **Tès `Architecture/LayerDependencyTests`:** yo verifye règ depandans sa yo otomatikman.

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

## 3. Konfigirasyon API a (sekrè)

Tout sekrè yo rete nan **user-secrets** pandan devlopman, epi nan **varyab anviwònman** nan lòt anviwònman yo. Yo pa janm nan `appsettings*.json`.

```bash
cd backend/src/Imotek.API

# Baz done (valè yo dwe matche ak .env)
dotnet user-secrets set "ConnectionStrings:ImotekDb" "Host=localhost;Port=5433;Database=imotek_dev;Username=imotek;Password=<POSTGRES_PASSWORD>;Timeout=5"

# Kont administratè (kreye otomatikman lè API a demare)
dotnet user-secrets set "Seed:AdminEmail" "admin@imotek.local"
dotnet user-secrets set "Seed:AdminPassword" "<yon modpas solid>"

# Pou wè valè yo
dotnet user-secrets list
```

| Kle | Varyab anviwònman | Obligatwa |
|-----|-------------------|-----------|
| `ConnectionStrings:ImotekDb` | `ConnectionStrings__ImotekDb` | Wi, API a refize demare san li |
| `Seed:AdminEmail` / `Seed:AdminPassword` | `Seed__AdminEmail` / `Seed__AdminPassword` | Non. Se sèl fason pou kreye yon Admin |

## 4. Migrasyon database

`dotnet-ef` 10 enstale kòm **zouti lokal** (`backend/dotnet-tools.json`), kidonk li pa depann de vèsyon global ki sou machin nan.

```bash
cd backend
dotnet tool restore

# Aplike migrasyon yo (obligatwa anvan premye lansman an)
dotnet ef database update -p src/Imotek.Infrastructure -s src/Imotek.API

# Kreye yon nouvo migrasyon
dotnet ef migrations add <NonMigrasyon> -p src/Imotek.Infrastructure -s src/Imotek.API -o Persistence/Migrations

# Lis migrasyon yo
dotnet ef migrations list -p src/Imotek.Infrastructure -s src/Imotek.API
```

| Migrasyon | Kontni |
|-----------|--------|
| `InitialBaseline` | Vid (tab `__EFMigrationsHistory`) |
| `AddIdentityAndProperties` | Tab Identity yo (`AspNetUsers`, `AspNetRoles`, …), 3 wòl yo (`Admin`, `Agent`, `Client`), ak tab `Properties` la. `OwnerId` gen yon FK sou `AspNetUsers` ak `ON DELETE RESTRICT`. |

## 5. Lanse aplikasyon an

```bash
# 1. PostgreSQL
docker compose up -d

# 2. API
cd backend
dotnet run --project src/Imotek.API --launch-profile http

# 3. Frontend (nan yon lòt terminal)
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

### Paj frontend yo

| Chemen | Paj | Aksè |
|--------|-----|------|
| `/` | Dakèy, rechèch rapid, dènye biens disponib | Tout moun |
| `/biens` | Lis biens disponib ak filtè (lokalite, depatman, tip, lwe/vann, pri, devise, mo kle) | Tout moun |
| `/biens/:id` | Detay, foto, pri | Tout moun (Draft/Rented/Sold: sèlman pwopriyetè a ak Admin) |
| `/connexion`, `/inscription` | Koneksyon, kreye kont (Agent oswa Client) | Tout moun |
| `/espace-agent` | Lis biens ajan an, estati yo, modifye, efase | Agent |
| `/espace-agent/biens/nouveau`, `/espace-agent/biens/:id/modifier` | Fòm kreye / modifye | Agent |

## 6. API

Repons erè yo se ProblemDetails (RFC 9457). Erè validasyon yo (`400`) gen kle camelCase nan `errors`.

### Otantifikasyon

| Metòd | Chemen | Deskripsyon |
|-------|--------|-------------|
| `POST` | `/api/auth/register` | `{ email, password, fullName, role: "Agent" \| "Client" }`. Wòl Admin pa ka chwazi la. |
| `POST` | `/api/auth/login` | `{ email, password }` → `{ accessToken, expiresIn, refreshToken }`. Token an valab 1 è. |
| `GET` | `/api/auth/me` | Itilizatè ki konekte a ak wòl li yo |

Voye token an nan header `Authorization: Bearer <accessToken>`.

### Properties

| Metòd | Chemen | Aksè | Nòt |
|-------|--------|------|-----|
| `GET` | `/api/properties` | Piblik | Sèlman `Available`. Filtè: `search`, `location`, `department`, `propertyType`, `transactionType`, `minPrice`, `maxPrice`, `currency`, `page`, `pageSize` (≤ 50) |
| `GET` | `/api/properties/{id}` | Piblik | `404` si bien an pa `Available`, sof pou pwopriyetè a ak Admin |
| `GET` | `/api/properties/my-properties` | Agent | Tout biens ajan an, nan tout estati yo |
| `POST` | `/api/properties` | Agent | `OwnerId` soti nan token an. Si body a voye youn, API a pa konsidere l. |
| `PUT` | `/api/properties/{id}` | Agent (pwopriyetè) | |
| `DELETE` | `/api/properties/{id}` | Agent (pwopriyetè) | |
| `GET` | `/api/admin/properties` | Admin | Tout biens, tout estati (`?status=` pou filtre) |
| `PUT` | `/api/admin/properties/{id}` | Admin | |
| `DELETE` | `/api/admin/properties/{id}` | Admin | |

**Kòd estati:**

| Kòd | Kilè |
|-----|------|
| `401` | Pa gen token, oswa token an pa valab |
| `403` | Itilizatè a pa gen wòl ki nesesè a (pa egzanp yon Client ki eseye kreye), oswa li ap manipile yon bien `Available` ki pa pou li |
| `404` | Bien an pa egziste, oswa se yon bien `Draft`, `Rented` oswa `Sold` ki pa pou li. Nan dezyèm ka a, API a kache egzistans bien an. |

Ou ka jwenn egzanp demann nan [`Imotek.API.http`](backend/src/Imotek.API/Imotek.API.http).

### Health checks

| Endpoint | Sa li verifye | Repons |
|----------|---------------|--------|
| `GET /health` | API a ap kouri (pa teste DB a) | `200` |
| `GET /health/db` | Koneksyon reyèl ak PostgreSQL | `200` si DB a disponib, `503` si li pa disponib |

## 7. Tès

```bash
cd backend
dotnet test --solution Imotek.slnx
```

| Gwoup | Sa li teste | DB |
|-------|-------------|----|
| `Domain` | Règ `Property` yo (validasyon, timestamps, vizibilite) | Non |
| `Application` | Otorizasyon: kreye, modifye, efase (pwopriyetè, lòt ajan, Admin, Client, vizitè), vizibilite, rechèch piblik ak filtè yo | Non (repository an memwa) |
| `Architecture` | Règ depandans ant kouch yo | Non |
| `Infrastructure` | Enskripsyon DI, provider Npgsql, health check | Non |
| `Api/HealthEndpointsTests` | `/health`, `/health/db` 503, san fwit enfòmasyon | Non |
| `Api/PropertiesApiTests` | HTTP ak PostgreSQL reyèl: migrasyon, 401/403/404, OwnerId, Admin, filtè SQL | **Testcontainers** |

`PropertiesApiTests` lanse yon kontenè PostgreSQL tanporè. Li pa itilize baz done devlopman an, ni okenn sekrè. **Fòk Docker ap kouri.** Si Docker pa disponib, tès sa yo sote (*skipped*) epi yo bay rezon an: yo pa echwe.

> `global.json` aktive Microsoft.Testing.Platform pou `dotnet test` (obligatwa pou xUnit v3 sou .NET 10 SDK).

## 8. Build

```bash
cd backend  && dotnet build -c Release
cd frontend && npm run build
cd frontend && npm run lint
```
