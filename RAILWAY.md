# Déploiement d'Imotek sur Railway

Guide de configuration pour trois services Railway dans un même projet et un même environnement :

| Service | Source | Rôle | Exposition |
|---------|--------|------|------------|
| `imotek-db` | Template PostgreSQL Railway | Base de données | Privée uniquement |
| `imotek-api` | GitHub, dossier `/backend` (Dockerfile) | API ASP.NET Core .NET 10 | Privée uniquement |
| `imotek-web` | GitHub, dossier `/frontend` (Dockerfile) | React + nginx, proxy vers l'API | Domaine public |

> `compose.prod.yaml` sert aux tests locaux et à un hébergement Docker classique. Railway ne l'utilise pas :
> chaque service est configuré séparément dans le Dashboard.

## Architecture

```
Navigateur ──HTTPS──► Edge Railway (TLS, X-Real-IP, X-Forwarded-Proto)
                         │
                         ▼
                  imotek-web (nginx, $PORT)
                  ├── /            → fichiers React (fallback index.html)
                  └── /api, /media, /health
                         │  réseau privé : http://imotek-api.railway.internal:8080
                         ▼
                  imotek-api (Kestrel, $PORT, volume /app/data)
                         │  réseau privé : DATABASE_URL
                         ▼
                  imotek-db (PostgreSQL, volume Railway)
```

Le navigateur ne parle qu'à `imotek-web`. Ce choix a plusieurs conséquences :
- **Même origine :** pas de CORS à ouvrir, CSP `connect-src 'self'`, et aucune URL d'API compilée dans le bundle (pas de `VITE_*`).
- **API non exposée :** l'API n'a **pas de domaine public**, donc personne ne peut la joindre directement ni falsifier les en-têtes de proxy.
- **Résolution à la volée :** nginx résout le nom privé de l'API à chaque requête (cache de 10 s). Il démarre même si l'API n'est pas prête et suit sa nouvelle adresse après un redéploiement.

## 1. imotek-db

1. **New → Database → PostgreSQL**, puis renommer le service en `imotek-db`.
2. **Ne pas** activer *Public Access* (TCP Proxy). La base reste privée et il n'y a pas d'egress facturé.
3. **Backups** (onglet du service) : activer au moins une planification **Daily**. Les volumes ne sont **pas** sauvegardés automatiquement tant qu'aucune planification n'existe, et la fonctionnalité est récente.

## 2. imotek-api

### Paramètres du service

| Paramètre | Valeur |
|-----------|--------|
| Source | Dépôt GitHub `Jeff-44/imotek`, branche `main` |
| Root Directory | `/backend` |
| Builder | Dockerfile (`backend/Dockerfile` détecté automatiquement). Railway construit la **dernière** étape, `api`. |
| Watch Paths | `/backend/**` |
| Pre-deploy Command | `/app/efbundle` |
| Healthcheck Path | `/health/db` (timeout par défaut : 300 s) |
| Restart Policy | On Failure |
| Replicas | **1**. Railway interdit plusieurs réplicas sur un service avec volume. |
| Volume | Un volume monté sur **`/app/data`** |
| Networking | **Pas de domaine public**. Le domaine privé `imotek-api.railway.internal` suffit. |

> La configuration « as code » (`railway.json`/`railway.toml`) est dépréciée par Railway : elle ne fonctionne
> plus pour les services legacy après le 2026-12-01. Ces paramètres sont donc à saisir dans le Dashboard.

### Variables

| Variable | Valeur | Remarque |
|----------|--------|----------|
| `DATABASE_URL` | `${{imotek-db.DATABASE_URL}}` | Référence Railway (réseau privé). Convertie automatiquement en chaîne Npgsql. |
| `PORT` | `8080` | Fixé explicitement pour que `imotek-web` puisse y faire référence |
| `RAILWAY_RUN_UID` | `0` | Le volume est monté en root. L'entrypoint prépare `/app/data` puis **redescend à l'utilisateur `app`** (uid 1654). Sans cette variable, l'API refuse de démarrer avec un message explicite. |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` | Fait confiance aux en-têtes `X-Forwarded-*` posés par nginx (IP client, HTTPS) |
| `Security__HttpsRedirection` | `false` | TLS est terminé par Railway, qui redirige déjà HTTP vers HTTPS |
| `AllowedHosts` | `<domaine-web>;healthcheck.railway.app` | Ex. `imotek-web-production.up.railway.app;healthcheck.railway.app`. Le healthcheck Railway utilise ce nom d'hôte. Ajouter le domaine personnalisé le moment venu. |
| `Cors__AllowedOrigins__0` | `https://<domaine-web>` | Origine exacte, sans `/` final. Pas indispensable en même origine, mais restrictif si l'API est un jour appelée directement. |
| `Seed__AdminEmail` | e-mail de l'administrateur | **Premier déploiement seulement**, puis supprimer |
| `Seed__AdminPassword` | mot de passe fort (≥ 8 caractères, majuscule, minuscule, chiffre) | **Premier déploiement seulement**, puis supprimer. L'API journalise un avertissement s'il reste configuré. |

Ces valeurs sont déjà définies dans l'image et n'ont pas à être saisies :

| Variable | Valeur |
|----------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `FileStorage__RootPath` | `/app/data/uploads` |
| `DataProtection__KeysPath` | `/app/data/keys` |

Réglages facultatifs (valeurs par défaut entre parenthèses) :

| Variable | Défaut |
|----------|--------|
| `Auth__AccessTokenMinutes` | `30` |
| `Auth__RefreshTokenDays` | `7` |
| `RateLimiting__AuthPermitLimit` | `10` |
| `RateLimiting__UploadPermitLimit` | `30` |

Tous les secrets (`DATABASE_URL`, `Seed__AdminPassword`) vont **uniquement** dans *Railway Variables*, jamais dans GitHub.

### Volume `/app/data`

Railway n'autorise qu'**un volume par service**. Les deux données persistantes de l'API partagent donc le même volume :

| Chemin | Contenu | Droits |
|--------|---------|--------|
| `/app/data/uploads` | Photos des biens (`properties/<propertyId>/<imageId>.jpg/png/webp`) | `app`, 755 |
| `/app/data/keys` | Clés ASP.NET Core Data Protection, qui chiffrent les tokens | `app`, 700 |

Comportement vérifié localement en reproduisant les règles Railway :

| Situation | Résultat |
|-----------|----------|
| Redémarrage du conteneur | Tokens, refresh tokens et photos conservés |
| Redéploiement (nouveau conteneur, même volume) | Tokens, refresh tokens et photos conservés |
| Démarrage sans `RAILWAY_RUN_UID=0` | Arrêt immédiat avec un message clair |

Limites à connaître :
- **Coupure au redéploiement :** Railway annonce une courte indisponibilité à chaque redéploiement d'un service avec volume, même avec healthcheck.
- **Une seule instance :** pas de réplica possible. Pour en avoir plusieurs, il faudra déplacer les photos vers un stockage S3-compatible (l'interface `IFileStorage` est prête), et les clés Data Protection vers un stockage partagé (base de données ou S3) chiffré.
- **Clés non chiffrées :** les clés Data Protection sont stockées en clair sur le volume. L'API l'indique par l'avertissement « No XML encryptor configured ».
- **Restauration limitée :** une restauration de backup n'est possible que dans le même projet et le même environnement, et supprimer (*wipe*) un volume supprime aussi ses backups.

## 3. imotek-web

| Paramètre | Valeur |
|-----------|--------|
| Root Directory | `/frontend` |
| Builder | Dockerfile (`frontend/Dockerfile`) |
| Watch Paths | `/frontend/**` |
| Healthcheck Path | `/healthz` (répond par nginx seul, sans dépendre de l'API) |
| Networking | **Generate Domain**. Le port cible est celui de `PORT`. |

| Variable | Valeur | Remarque |
|----------|--------|----------|
| `API_INTERNAL_URL` | `http://${{imotek-api.RAILWAY_PRIVATE_DOMAIN}}:${{imotek-api.PORT}}` | Réseau privé, en `http://` |
| `NGINX_TRUST_X_REAL_IP` | `on` | L'edge Railway fournit `X-Real-IP`, utilisé comme IP client pour le rate limiting. Laisser `off` hors Railway. |

`PORT` est injecté par Railway et nginx l'écoute en IPv4 et IPv6. Aucune variable `VITE_*` n'est nécessaire et aucun secret n'est compilé dans le frontend.

## 4. Migrations

La **Pre-deploy Command** `/app/efbundle` applique les migrations EF Core en attente. D'après la documentation Railway :
- **Moment :** elle s'exécute **entre le build et le déploiement**, dans un conteneur séparé qui a accès au réseau privé et aux variables, mais pas au volume.
- **En cas d'échec :** la commande n'est pas relancée et **le déploiement ne se fait pas**. L'ancienne version continue de servir.

Sécurité de la procédure :

| Risque | Protection |
|--------|------------|
| Migrations simultanées | Exécution hors de l'API, et une seule instance (volume = pas de réplica) |
| Ré-exécution | Le bundle est idempotent : relancé, il n'applique rien de plus (vérifié) |
| Migration destructive | `MigrationSafetyTests` échoue si une migration fait `DROP`, `DELETE`, `TRUNCATE`, un renommage ou un `ALTER COLUMN` |
| Données | Le bundle lit `DATABASE_URL` ; il ne lance pas l'API ni la création de l'administrateur |

Sur une base vierge, EF journalise une erreur `relation "__EFMigrationsHistory" does not exist` avant de créer la table. Ce message est normal.

**Avant toute version qui contient une nouvelle migration :**
1. Créer un **backup manuel** du volume `imotek-db`, depuis l'onglet Backups.
2. Relire le SQL :
   ```bash
   dotnet ef migrations script --idempotent -p src/Imotek.Infrastructure -s src/Imotek.API -o migrate.sql
   ```
3. Vérifier que `dotnet test` passe, y compris `MigrationSafetyTests`.

**Exécution manuelle, si besoin** (depuis un poste qui a accès à la base, par exemple via un TCP proxy activé temporairement) :
```bash
cd backend
dotnet ef database update -p src/Imotek.Infrastructure -s src/Imotek.API --connection "<chaîne de connexion>"
```

## 5. Sauvegarde et restauration

| Donnée | Où | Sauvegarde |
|--------|----|------------|
| Base de données | Volume de `imotek-db` | Backups Railway planifiés + `pg_dump` périodique hors Railway |
| Photos | `/app/data/uploads` (volume de `imotek-api`) | Backups Railway du volume `imotek-api` |
| Clés Data Protection | `/app/data/keys` (même volume) | Même backup. En cas de perte, tous les utilisateurs doivent se reconnecter ; aucune donnée métier n'est perdue. |

Pour un dump hors Railway, activer temporairement *Public Access* sur `imotek-db`, puis :
```bash
pg_dump "<DATABASE_PUBLIC_URL>" -Fc -f imotek-$(date +%F).dump
```
Ensuite, désactiver *Public Access*. **Tester une restauration** sur un environnement séparé avant l'ouverture au public.

## 6. Étapes dans le Dashboard Railway

1. Créer un projet et un environnement `production`.
2. Ajouter PostgreSQL, le renommer `imotek-db`, activer des backups planifiés (section 1).
3. Ajouter `imotek-api` depuis GitHub. Régler Root Directory, Watch Paths, Pre-deploy Command, Healthcheck, le volume `/app/data` et les variables (section 2). **Ne pas** générer de domaine public.
4. Ajouter `imotek-web` depuis GitHub. Régler Root Directory, Watch Paths, Healthcheck, les variables (section 3), puis **Generate Domain**.
5. Reporter le domaine de `imotek-web` dans `AllowedHosts` et `Cors__AllowedOrigins__0` de `imotek-api`, puis redéployer l'API.
6. Après le premier démarrage réussi (logs « Admin account is ready. »), **supprimer** `Seed__AdminEmail` et `Seed__AdminPassword`, puis redéployer.
7. Vérifier :
   - `https://<domaine-web>/health/db` répond `200` ;
   - une page interne rechargée (`/biens`) s'affiche ;
   - la connexion admin fonctionne ;
   - une photo importée s'affiche.
8. Activer les backups planifiés sur le volume de `imotek-api`.
9. Mettre en place une surveillance externe (uptime). Le healthcheck Railway n'est utilisé qu'au déploiement.
