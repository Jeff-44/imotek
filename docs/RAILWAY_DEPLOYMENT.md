# Imotek — Premye deployment sou Railway (etap manyèl)

Gid sa a gen sèlman sa pou fè nan **Railway Dashboard**. Pa gen anyen pou chanje nan kòd la.
Detay teknik yo (poukisa chak paramèt la) rete nan [`RAILWAY.md`](../RAILWAY.md).

Twa sèvis nan menm pwojè a ak menm environment (`production`):

| Sèvis | Sous | Domèn piblik |
|---|---|---|
| `imotek-db` | Template PostgreSQL Railway | Non |
| `imotek-api` | GitHub `Jeff-44/imotek`, dosye `/backend` | **Non** |
| `imotek-web` | GitHub `Jeff-44/imotek`, dosye `/frontend` | **Wi** (sèl sèvis piblik la) |

Navigatè a pale sèlman ak `imotek-web`; nginx voye `/api`, `/media` ak `/health` bay API a sou rezo prive a.

---

## 0. Anvan ou kòmanse

Railway bati kòd ki sou **GitHub**. Kounye a `origin/main` toujou sou `d6e6a9e`, **san MVP a**:
tout travay la sou branch lokal `feature/step-1-agent-approval-phone` (dènye commit `c27c68c`).
Avan deployment, pouse branch lan (oswa fè merge nan `main`) sou GitHub, epi chwazi **menm branch sa a**
kòm sous nan sèvis `imotek-api` ak `imotek-web`.

Prepare tou:
- Imèl Admin lan (ex. `admin@imotek.ht`).
- Yon modpas Admin solid: **omwen 8 karaktè, yon majiskil, yon miniskil, yon chif**. Pa mete l okenn lòt kote apa Railway Variables.

---

## 1. Kreye PostgreSQL — `imotek-db`

1. **New → Database → PostgreSQL**. Renome sèvis la `imotek-db`.
2. Kite *Public Access* (TCP Proxy) **dezaktive**.
3. Onglet **Backups**: aktive yon plan **Daily**.

## 2. Kreye API a — `imotek-api`

1. **New → GitHub Repo → `Jeff-44/imotek`**. Renome sèvis la `imotek-api`.
2. **Settings**:

| Paramèt | Valè |
|---|---|
| Source branch | branch ou te pouse a (gade etap 0) |
| Root Directory | `/backend` |
| Builder | Dockerfile (Railway jwenn `backend/Dockerfile` pou kont li) |
| Watch Paths | `/backend/**` |
| Pre-deploy Command | `/app/efbundle` |
| Healthcheck Path | `/health/db` |
| Restart Policy | On Failure |
| Replicas | `1` |

3. **Volume**: *New Volume* sou `imotek-api`, mount path **`/app/data`** (foto yo ak kle sesyon yo).
4. **Networking**: **pa jenere okenn domèn piblik**. Domèn prive `imotek-api.railway.internal` la sifi.

## 3. Kreye Web la — `imotek-web`

1. **New → GitHub Repo → `Jeff-44/imotek`**. Renome sèvis la `imotek-web`.
2. **Settings**:

| Paramèt | Valè |
|---|---|
| Source branch | menm branch ak API a |
| Root Directory | `/frontend` |
| Builder | Dockerfile (`frontend/Dockerfile`) |
| Watch Paths | `/frontend/**` |
| Healthcheck Path | `/healthz` |

3. **Networking → Generate Domain**. Note domèn nan, ex. `imotek-web-production.up.railway.app`.
   Nan tablo ki anba yo, `<WEB_DOMAIN>` = domèn sa a, **san** `https://`.

## 4. Variables

### `imotek-api`

| Variable | Valè | Obligatwa |
|---|---|---|
| `DATABASE_URL` | `${{imotek-db.DATABASE_URL}}` | Wi |
| `PORT` | `8080` | Wi |
| `RAILWAY_RUN_UID` | `0` | Wi (san li, API a refize demare) |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` | Wi |
| `Security__HttpsRedirection` | `false` | Wi |
| `AllowedHosts` | `<WEB_DOMAIN>;healthcheck.railway.app` | Wi |
| `Cors__AllowedOrigins__0` | `https://<WEB_DOMAIN>` | Opsyonèl (rekòmande) |
| `Seed__AdminEmail` | imèl Admin lan | **Premye deployment sèlman** |
| `Seed__AdminPassword` | modpas Admin lan | **Premye deployment sèlman** |

Pa mete: `ASPNETCORE_ENVIRONMENT`, `FileStorage__RootPath`, `DataProtection__KeysPath` (yo deja nan imaj la).

Opsyonèl, valè default yo bon pou lansman an: `Auth__AccessTokenMinutes` (30), `Auth__RefreshTokenDays` (7),
`Auth__SecurityStampCache__Seconds` (30), `RateLimiting__AuthPermitLimit` (10), `RateLimiting__UploadPermitLimit` (30),
`RateLimiting__ContactPermitLimit` (30), `RateLimiting__RequestPermitLimit` (5).

### `imotek-web`

| Variable | Valè | Obligatwa |
|---|---|---|
| `API_INTERNAL_URL` | `http://${{imotek-api.RAILWAY_PRIVATE_DOMAIN}}:${{imotek-api.PORT}}` | Wi |
| `NGINX_TRUST_X_REAL_IP` | `on` | Wi |

`PORT` sou `imotek-web` la: Railway mete l pou kont li, pa ajoute l.

### Rezo prive

Pa gen anyen pou aktive: sèvis yo nan menm pwojè ak menm environment pale youn ak lòt sou rezo prive Railway la
gras ak referans `${{...}}` yo pi wo a. Verifye sèlman ke **sèl** `imotek-web` gen yon domèn piblik.

## 5. Deploy

1. Deploy `imotek-db`, epi `imotek-api`, epi `imotek-web`.
2. Nan **Deployments → Logs** `imotek-api`:
   - Pre-deploy: `Applying migration '…'` 6 fwa, epi `Done.`
     (sou yon baz vid, yon erè `relation "__EFMigrationsHistory" does not exist` parèt anvan — sa nòmal).
   - API: **`Admin account is ready.`**

## 6. Premye login Admin

1. Louvri `https://<WEB_DOMAIN>/connexion`.
2. Konekte ak **`Seed__AdminEmail`** ak **`Seed__AdminPassword`** ou te mete nan Railway.
3. Ou dwe ateri sou paj **Administration**.

## 7. Retire variables bootstrap yo (apre premye login an reyisi)

1. `imotek-api` → Variables: **efase** `Seed__AdminEmail` ak `Seed__AdminPassword`.
2. Redeploy `imotek-api`.
3. Konekte ankò ak menm imèl ak modpas: kont Admin lan rete, li pa chanje.

Sa ki verifye lokalman ak imaj prodiksyon yo:
- Admin lan kreye **yon sèl fwa**; si li deja egziste, modpas li **pa janm** chanje pa variables yo.
- Tank `Seed__AdminPassword` rete, API a ekri yon avètisman *"Seed:AdminPassword is still configured…"* nan log yo (san modpas la).
- San variables yo, API a demare nòmalman e Admin lan konekte toujou.

## 8. Apre lansman

- Aktive **Backups** sou volume `imotek-api` tou (foto yo).
- Si ou ajoute yon domèn pèsonèl sou `imotek-web`: ajoute l nan `AllowedHosts` (separe ak `;`) ak nan `Cors__AllowedOrigins__1` sou `imotek-api`, epi redeploy API a.

---

## Checklist smoke test apre deployment

Fè yo nan ti lòd sa a, sou `https://<WEB_DOMAIN>`:

| # | Tès | Rezilta atann |
|---|---|---|
| 1 | Louvri sit la, epi rechaje sou `/biens` | Paj yo parèt (pa gen 404) |
| 2 | `https://<WEB_DOMAIN>/health/db` | `200` (API + baz done konekte atravè proxy Web la) |
| 3 | Login Admin (etap 6) | Paj Administration |
| 4 | Kreye yon kont « Publier des annonces », Admin apwouve l nan Administration | Agent lan konekte, li wè « + Ajouter un bien » |
| 5 | Agent kreye yon anons `Disponible` epi ajoute yon foto | Foto a parèt |
| 6 | Nan yon fenèt prive (san kont): `/biens` | Anons lan parèt |
| 7 | Sou detay anons lan | Nimewo Agent lan + Appeler / WhatsApp / SMS |
| 8 | Toujou san kont: « Demander une visite » (oswa « Demander une location ») | Konfimasyon + lyen `/suivi#…` |
| 9 | Agent → « Demandes reçues » | Demann lan, ak non, imèl, telefòn vizitè a |
| 10 | Kreye yon kont kliyan, konekte, gade « Mes demandes » | Mache |
| 11 | Konekte menm kliyan an sou 2 navigatè, dekonekte sou youn | Lòt la retounen sou paj koneksyon nan pwochen aksyon li |
| 12 | Railway → `imotek-api` → **Restart**, epi rechaje anons lan | Foto a, anons lan ak demann lan toujou la; sesyon yo toujou valab |

Si tès 2 bay `502`/`504`: verifye `API_INTERNAL_URL` sou `imotek-web` ak ke `imotek-api` *Active*.
Si API a pa demare: verifye `RAILWAY_RUN_UID=0` ak volume `/app/data`.
Si healthcheck API a echwe: verifye `AllowedHosts` gen `healthcheck.railway.app`.
