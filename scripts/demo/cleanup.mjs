// Removes every demo record created by seed.mjs, and nothing else: the accounts ending with
// @demo.imotek.test, their listings and photo files, and the requests and notifications about them.
//
//   node scripts/demo/cleanup.mjs            (lists what would be removed)
//   node scripts/demo/cleanup.mjs --confirm  (removes it)
//
// Runs SQL through the local PostgreSQL container (IMOTEK_DB_CONTAINER, default imotek-postgres).

import { execFileSync } from 'node:child_process'
import { existsSync, rmSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const container = process.env.IMOTEK_DB_CONTAINER ?? 'imotek-postgres'
const uploads = resolve(dirname(fileURLToPath(import.meta.url)), '../../backend/src/Imotek.API/data/uploads/properties')
const confirm = process.argv.includes('--confirm')

const psql = (sql) =>
  execFileSync('docker', ['exec', '-i', container, 'sh', '-c', 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -v ON_ERROR_STOP=1'], {
    input: sql,
    encoding: 'utf8',
  }).trim()

const demoUsers = `SELECT "Id" FROM "AspNetUsers" WHERE "Email" LIKE '%@demo.imotek.test'`
const demoProperties = `SELECT "Id" FROM "Properties" WHERE "OwnerId" IN (${demoUsers})`
const requestFilter = `"ClientId" IN (${demoUsers}) OR "OwnerId" IN (${demoUsers}) OR "PropertyId" IN (${demoProperties})`

const emails = psql(`SELECT "Email" FROM "AspNetUsers" WHERE "Email" LIKE '%@demo.imotek.test' ORDER BY 1;`)
const propertyIds = psql(`${demoProperties};`).split('\n').filter(Boolean)
const counts = psql(`SELECT
  (SELECT count(*) FROM "RentalRequests" WHERE ${requestFilter}) || ' rental, ' ||
  (SELECT count(*) FROM "PurchaseRequests" WHERE ${requestFilter}) || ' purchase, ' ||
  (SELECT count(*) FROM "VisitRequests" WHERE ${requestFilter}) || ' visit requests';`)

if (!emails) {
  console.log('No demo data found.')
  process.exit(0)
}

console.log(`Accounts:\n  ${emails.split('\n').join('\n  ')}`)
console.log(`Listings: ${propertyIds.length}`)
console.log(`Requests: ${counts}`)

if (!confirm) {
  console.log('\nNothing removed. Run again with --confirm to delete these records.')
  process.exit(0)
}

// Order matters: requests and notifications reference listings and accounts.
psql(`BEGIN;
DELETE FROM "Notifications" WHERE "UserId" IN (${demoUsers}) OR "PropertyId" IN (${demoProperties});
DELETE FROM "RentalRequests" WHERE ${requestFilter};
DELETE FROM "PurchaseRequests" WHERE ${requestFilter};
DELETE FROM "VisitRequests" WHERE ${requestFilter};
DELETE FROM "Properties" WHERE "Id" IN (${demoProperties});
DELETE FROM "AspNetUsers" WHERE "Id" IN (${demoUsers});
COMMIT;`)

for (const id of propertyIds) {
  const folder = join(uploads, id.replaceAll('-', ''))
  if (existsSync(folder)) rmSync(folder, { recursive: true })
}

console.log('\nDemo data removed.')
