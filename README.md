# Imotek

Aplikasyon web immobilier pou Ayiti.

| Kouch | Teknoloji |
|-------|-----------|
| Backend | ASP.NET Core (.NET 10 LTS), REST API (controllers) |
| Achitekti | Clean Architecture |
| ORM | Entity Framework Core 10 + Npgsql |
| Baz done | PostgreSQL 18 (Docker) |
| Otantifikasyon | ASP.NET Core Identity + bearer token ak refresh token, wòl `Admin` / `Agent` / `Client` |
| Foto | Upload ki verifye sou sèvè a, san metadata, nan yon `IFileStorage` (disk lokal pou kounye a) |
| Tès | xUnit v3 (Microsoft.Testing.Platform), Testcontainers PostgreSQL, Vitest |
| Frontend | React 19 + TypeScript + Vite, react-router 7 |
| Prodiksyon | Docker Compose: PostgreSQL, travay migrasyon, API, nginx |

## Estrikti repository a

Pwojè a se yon dosye nan workspace `ai-dev` la, ki ka gen lòt pwojè tou. Chak pwojè gen pwòp depo Git pa l. Memwa workspace la (`ai-dev/MEMORY.md`) rete deyò depo sa a.

```
imotek/                              # Rasin depo Git la
├── compose.yaml                     # PostgreSQL pou devlopman
├── compose.prod.yaml                # Stack prodiksyon (postgres, migrate, api, web)
├── .env.example / .env.prod.example # Modèl varyab anviwònman
├── backend/
│   ├── Dockerfile                   # Imaj api + migrator (EF bundle)
│   ├── Imotek.slnx, global.json, dotnet-tools.json
│   ├── src/
│   │   ├── Imotek.Domain/           # Property, PropertyImage, SearchText, règ validasyon
│   │   ├── Imotek.Application/      # PropertyService (otorizasyon), IFileStorage, IImageProcessor, DTO   → Domain
│   │   ├── Imotek.Infrastructure/   # EF Core, Identity, ImageProcessor, LocalFileStorage, migrasyon      → Application, Domain
│   │   └── Imotek.API/              # Controllers, Configuration/ApiSecurity, health checks              → Application, Infrastructure
│   └── tests/Imotek.Tests/          # Domain, Application, Architecture, Infrastructure, Api
├── frontend/
│   ├── Dockerfile, nginx/           # Build Vite + nginx non-root
│   └── src/                         # api/, auth/, components/, pages/ (+ *.test.ts)
├── .gitignore
└── README.md
```

### Règ depandans (Clean Architecture)

```
API ──► Application ──► Domain
 │           ▲
 └──► Infrastructure ──┘ (+ Domain)
```

- **Règ biznis yo nan Domain:** règ pwopriyete yo (`Property.Validate`) ak nòmalizasyon tèks la (`SearchText`) pa depann de okenn framework.
- **Otorizasyon nan Application:** tout règ otorizasyon ak vizibilite yo nan `PropertyService`. Kidonk yo aplike menm jan kèlkeswa endpoint lan.
- **Controllers mens:** yo sèlman tradui `Result` yo an kòd HTTP.
- **Tès otomatik:** `Architecture/` verifye depandans ant kouch yo. Li verifye tou ke okenn migrasyon pa efase done (`MigrationSafetyTests`).

## Sekirite

| Sijè | Sa ki an plas |
|------|---------------|
| Token | Access token 30 min, refresh token 7 jou (`POST /api/auth/refresh`). Frontend la renouvle token an otomatikman. |
| Dekoneksyon | `POST /api/auth/logout` chanje *security stamp* itilizatè a, sa ki anile tout refresh token li yo sou tout aparèy. |
| Kle token yo | Kle Data Protection yo estoke nan yon volume (`DataProtection:KeysPath`), kidonk sesyon yo pa disparèt lè kontenè a rekòmanse. |
| Otorizasyon | Wòl yo verifye nan controllers yo (`[Authorize(Roles)]`) **ak** nan `PropertyService`. `OwnerId` toujou soti nan token an. |
| Vizibilite | Yon bien `Draft`, `Rented` oswa `Sold` retounen **404** pou vizitè, kliyan, ak lòt ajan, menm lè yo rele `GET /api/properties/{id}` dirèkteman. |
| Brute force | *Rate limiting* sou login, register ak refresh: 10 demann pa minit pa IP. Kont lan bloke 15 min apre 5 move modpas. |
| Foto | Fòma a detekte nan premye bytes fichye a, pa nan non an. Sèlman JPEG, PNG ak WebP aksepte, 5 MB maksimòm. EXIF, GPS, XMP ak kòmantè yo retire. Non fichye a jenere pa sèvè a. |
| Header | API: `nosniff`, `X-Frame-Options: DENY`, `CSP default-src 'none'`, HSTS (prod). nginx: CSP `default-src 'self'`, `nosniff`, `server_tokens off`. |
| CORS | Fèmen pa defo. Pou louvri l, mete orijin presi yo nan `Cors:AllowedOrigins`. API a refize demare si gen yon `*`. |
| Erè | ProblemDetails. Pa gen okenn stack trace, connection string, oswa mesaj eksepsyon nan repons yo. |
| Admin | Yo pa ka enskri atravè API a. Kont lan kreye yon sèl fwa lè API a demare, ak `Seed:AdminEmail` / `Seed:AdminPassword`. Modpas la pa janm reyinisyalize, epi logs yo bay yon avètisman si li toujou konfigire. |

## Prerekizit

- .NET SDK 10.0.x
- Node.js `^20.19` oswa `>=22.12` (Vite 8, Vitest 5)
- Docker Desktop (Docker Compose v2)

## 1. Varyab anviwònman (devlopman)

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

`.env` ak `.env.prod` eskli nan Git. Se sèlman fichye `*.example` yo ki commit.

## 2. Demare PostgreSQL ak Docker

```bash
docker compose up -d          # demare
docker compose ps             # estati (dwe montre "healthy")
docker compose stop           # kanpe san efase done yo
```

**Pa itilize `docker compose down -v`**: opsyon `-v` la efase volume nan ak tout done yo.

## 3. Konfigirasyon API a

Tout sekrè yo rete nan **user-secrets** pandan devlopman, epi nan **varyab anviwònman** nan prodiksyon. Yo pa janm nan `appsettings*.json`.

```bash
cd backend/src/Imotek.API
dotnet user-secrets set "ConnectionStrings:ImotekDb" "Host=localhost;Port=5433;Database=imotek_dev;Username=imotek;Password=<POSTGRES_PASSWORD>;Timeout=5"
dotnet user-secrets set "Seed:AdminEmail" "admin@imotek.local"
dotnet user-secrets set "Seed:AdminPassword" "<yon modpas solid>"
```

| Kle (`appsettings`) | Varyab anviwònman | Default | Wòl |
|---------------------|-------------------|---------|-----|
| `ConnectionStrings:ImotekDb` | `ConnectionStrings__ImotekDb` | — (li oswa `DATABASE_URL`) | PostgreSQL |
| `DATABASE_URL` | `DATABASE_URL` | — | Altènativ: URL `postgresql://user:pass@host:port/db` (Railway). API a konvèti l otomatikman. |
| `Seed:AdminEmail`, `Seed:AdminPassword` | `Seed__AdminEmail`, `Seed__AdminPassword` | — | Kreye Admin lan (premye demaraj sèlman) |
| `Auth:AccessTokenMinutes` | `Auth__AccessTokenMinutes` | `30` | Dire access token |
| `Auth:RefreshTokenDays` | `Auth__RefreshTokenDays` | `7` | Dire refresh token |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, `__1`… | vid (fèmen) | Orijin navigatè ki otorize |
| `RateLimiting:AuthPermitLimit` | `RateLimiting__AuthPermitLimit` | `10` | Demann otantifikasyon pa minit pa IP |
| `RateLimiting:UploadPermitLimit` | `RateLimiting__UploadPermitLimit` | `30` | Upload pa minit pa itilizatè |
| `FileStorage:RootPath` | `FileStorage__RootPath` | `data/uploads` | Dosye foto yo (relatif ak content root API a) |
| `DataProtection:KeysPath` | `DataProtection__KeysPath` | — | Dosye kle token yo (obligatwa an prod) |
| `Security:HttpsRedirection` | `Security__HttpsRedirection` | `true` | Mete `false` lè yon proxy TLS deja fè redireksyon an |
| `AllowedHosts` | `AllowedHosts` | `*` | Non domèn ki otorize (fikse l an prod) |

## 4. Migrasyon database

`dotnet-ef` 10 enstale kòm **zouti lokal** (`backend/dotnet-tools.json`).

```bash
cd backend
dotnet tool restore
dotnet ef database update -p src/Imotek.Infrastructure -s src/Imotek.API                     # aplike
dotnet ef migrations add <Non> -p src/Imotek.Infrastructure -s src/Imotek.API -o Persistence/Migrations
dotnet ef migrations script --idempotent -p src/Imotek.Infrastructure -s src/Imotek.API -o migrate.sql   # SQL pou revize
```

| Migrasyon | Kontni |
|-----------|--------|
| `InitialBaseline` | Vid (tab `__EFMigrationsHistory`) |
| `AddIdentityAndProperties` | Tab Identity yo, 3 wòl yo, tab `Properties` |
| `AddSearchKeysAndPropertyImages` | Kolòn rechèch nòmalize yo (`SearchKey`, `LocationKey`, `DepartmentKey`) ak backfill yo, ak tab `PropertyImages` (FK cascade) |

**Règ:** migrasyon yo dwe ajoute sèlman (kolòn, tab, endèks). `MigrationSafetyTests` echwe si yon migrasyon fè `DROP`, `DELETE`, `TRUNCATE`, *rename* oswa `ALTER COLUMN`. Yon chanjman konsa mande yon revizyon espesyal, epi fòk li ajoute nan lis `ReviewedExceptions` la.

## 5. Lanse an devlopman

```bash
docker compose up -d
cd backend && dotnet run --project src/Imotek.API --launch-profile http
cd frontend && npm install && npm run dev        # lòt terminal
```

| Sèvis | URL |
|-------|-----|
| Frontend | http://localhost:5173 |
| API | http://localhost:5056 (OpenAPI: `/openapi/v1.json`) |

Vite voye `/api`, `/media` ak `/health` bay API a, menm jan nginx fè l an prodiksyon.

## 6. API

### Otantifikasyon

| Metòd | Chemen | Deskripsyon |
|-------|--------|-------------|
| `POST` | `/api/auth/register` | `{ email, password, fullName, role: "Agent" \| "Client" }` |
| `POST` | `/api/auth/login` | → `{ accessToken, expiresIn, refreshToken }` |
| `POST` | `/api/auth/refresh` | `{ refreshToken }` → nouvo token |
| `POST` | `/api/auth/logout` | Anile tout refresh token itilizatè a |
| `GET` | `/api/auth/me` | Itilizatè ki konekte a |

### Properties

| Metòd | Chemen | Aksè | Nòt |
|-------|--------|------|-----|
| `GET` | `/api/properties` | Piblik | Sèlman `Available`. Filtè: `search`, `location`, `department`, `propertyType`, `transactionType`, `minPrice`, `maxPrice`, `currency`, `page`, `pageSize` |
| `GET` | `/api/properties/{id}` | Piblik | `404` si bien an pa `Available`, sof pou pwopriyetè a ak Admin |
| `GET` | `/api/properties/my-properties` | Agent | Tout biens ajan an, nan tout estati yo |
| `POST` / `PUT` / `DELETE` | `/api/properties[/{id}]` | Agent (pwopriyetè) | |
| `POST` | `/api/properties/{id}/images` | Pwopriyetè, Admin | Multipart `file`: JPEG, PNG oswa WebP, 5 MB maksimòm |
| `DELETE` | `/api/properties/{id}/images/{imageId}` | Pwopriyetè, Admin | |
| `GET` | `/media/{key}` | Piblik | Foto ki upload yo (`Cache-Control: immutable`) |
| `GET` / `PUT` / `DELETE` | `/api/admin/properties[/{id}]` | Admin | |

**Rechèch:**
- Rechèch la pa sansib ni a aksan ni a majiskil: `petion` jwenn `Pétion-Ville`, epi `grand’anse` jwenn `Grand'Anse`.
- **Yon filtè pri bezwen yon devise** (`currency=HTG` oswa `USD`). Sinon API a retounen 400. Sistèm nan pa konvèti HTG an USD, paske pa gen okenn to echanj ki defini ak yon sous ofisyèl.

**Kòd estati:**
- `401`: pa gen token, oswa token an pa valab.
- `403`: itilizatè a pa gen wòl ki nesesè a, oswa li ap manipile yon bien vizib ki pa pou li.
- `404`: bien an pa egziste, oswa API a kache li.
- `413`: fichye a twò gwo.
- `429`: twòp demann.

## 7. Tès

```bash
cd backend  && dotnet test --solution Imotek.slnx     # 174 tès
cd frontend && npm test                                # 27 tès (Vitest)
```

| Gwoup | Sa li teste | DB |
|-------|-------------|----|
| `Domain` | Règ `Property`, `SearchText`, limit foto | Non |
| `Application` | Otorizasyon (pwopriyetè, lòt ajan, Admin, Client, vizitè), vizibilite, rechèch ak aksan, règ devise, upload foto | Non |
| `Architecture` | Depandans ant kouch yo, migrasyon ki pa efase done | Non |
| `Infrastructure` | DI, `ImageProcessor` (fòma, metadata, fichye mal fòme), `LocalFileStorage` (path traversal) | Non |
| `Api/SecurityTests` | Rate limiting, header yo, CORS, `/media` | Non |
| `Api/PropertiesApiTests`, `ProductionReadinessApiTests` | HTTP ak PostgreSQL reyèl: 401/403/404, Client ak bien kache, rechèch an SQL, upload, refresh/logout | **Testcontainers** (bezwen Docker, sinon tès yo *skipped*) |
| Frontend `*.test.ts` | Paramèt rechèch, tradiksyon erè, refresh token otomatik, verifikasyon fichye | Non |

## 8. Deplwaman pwodiksyon

> **Railway:** gade [RAILWAY.md](RAILWAY.md). Li genyen sèvis yo, varyab yo, volume `/app/data` la, migrasyon pre-deploy yo, ak etap pou Dashboard la. `compose.prod.yaml` la pa sèvi sou Railway.

### Docker Compose (sèvè Docker klasik oswa tès lokal)

```bash
cp .env.prod.example .env.prod      # ranplase TOUT valè yo
docker compose -f compose.prod.yaml --env-file .env.prod up -d --build
docker compose -f compose.prod.yaml --env-file .env.prod ps
```

Stack la:

| Sèvis | Wòl |
|-------|-----|
| `postgres` | Sou yon rezo `internal`, san okenn pò ekspoze |
| `migrate` | Bundle EF Core ki aplike migrasyon ki poko aplike yo, epi ki sispann apre. `api` la demare sèlman si li reyisi. |
| `api` | Non-root, logs JSON, health check |
| `web` | nginx non-root ki sèvi React epi ki voye `/api`, `/media` ak `/health` bay API a. Varyab li yo: `PORT`, `API_INTERNAL_URL`, `NGINX_TRUST_X_REAL_IP` (gade `frontend/nginx/default.conf.template`). |

Volume yo: `postgres-data`, `uploads`, `dataprotection-keys`.

**Anvan chak deplwaman:**
1. **Fè yon backup baz done a**, epi verifye li ka restore:
   ```bash
   docker compose -f compose.prod.yaml exec postgres pg_dump -U <user> -Fc <db> > backup.dump
   ```
2. **Revize SQL migrasyon yo** ak `dotnet ef migrations script --idempotent`. `MigrationSafetyTests` dwe pase.
3. Fè backup volume **`uploads`** la tou. Foto yo pa nan baz done a.

**TLS:** sèvis `web` la koute sèlman sou HTTP, sou `127.0.0.1:8080` pa defo. Fòk yon reverse proxy TLS ki devan li fè redireksyon HTTP → HTTPS, epi voye `X-Forwarded-Proto`. Egzanp: Caddy oswa nginx sou sèvè a ak Let's Encrypt, oswa load balancer founisè a.

**Apre premye demaraj la:** retire `ADMIN_EMAIL` ak `ADMIN_PASSWORD` nan `.env.prod`.

## 9. Build

```bash
cd backend  && dotnet build -c Release
cd frontend && npm run build && npm run lint
```
