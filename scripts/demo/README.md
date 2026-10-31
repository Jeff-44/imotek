# Demo data

Local test data to try the MVP: two agents, two clients, seven listings with photos, and requests in every state.
Everything is created through the API, with the same rules as the interface.

```bash
# API running on http://localhost:5056 (IMOTEK_API_URL to change it)
IMOTEK_ADMIN_EMAIL=... IMOTEK_ADMIN_PASSWORD=... node scripts/demo/seed.mjs

node scripts/demo/cleanup.mjs            # lists what would be removed
node scripts/demo/cleanup.mjs --confirm  # removes it
```

- Accounts end with `@demo.imotek.test` and share the password `Demo-Imotek-2026` (demo only, never in production).
- Listing titles start with `[Démo]`. Addresses are placeholders; there are no map coordinates.
- Phone numbers are in the fictional North American range `+1 201 555 01xx`, so a Call or WhatsApp button never reaches a real person.
- `cleanup.mjs` removes only the demo accounts, their listings, photo files, and the requests and notifications about them.

| Account | Role | What to look at |
|---|---|---|
| `pierre@demo.imotek.test` | Approved agent, number shown | 4 listings (one draft), received requests to answer |
| `nadia@demo.imotek.test` | Approved agent, consent withdrawn | Listings without a phone number, a declined visit |
| `marie@demo.imotek.test` | Client with a phone | Rental accepted, visit with a new date to accept, purchase pending |
| `jean@demo.imotek.test` | Client without a phone | Purchase answered, rental pending, visit declined |

Two requests also come from visitors without an account (a visit and a rental on Pierre's Pétion-Ville house).
Pierre sees them in "Demandes reçues" with the visitor's contact details; `seed.mjs` prints their private follow-up links (`/suivi/…`).
