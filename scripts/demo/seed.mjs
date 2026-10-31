// Demo data for local testing: agents, listings with photos, and requests in every state.
// Everything goes through the real API (same rules as the interface). Remove it with cleanup.mjs.
//
//   IMOTEK_ADMIN_EMAIL=... IMOTEK_ADMIN_PASSWORD=... node scripts/demo/seed.mjs
//
// Demo accounts end with @demo.imotek.test and listing titles start with "[Démo]".
// Phone numbers are in the North American fictional range 555-01xx: nobody can be reached by mistake.

import { deflateSync } from 'node:zlib'

const API = process.env.IMOTEK_API_URL ?? 'http://localhost:5056'
const ADMIN = { email: process.env.IMOTEK_ADMIN_EMAIL, password: process.env.IMOTEK_ADMIN_PASSWORD }
const DEMO_DOMAIN = 'demo.imotek.test'
const PASSWORD = 'Demo-Imotek-2026'

if (!ADMIN.email || !ADMIN.password) {
  console.error('Set IMOTEK_ADMIN_EMAIL and IMOTEK_ADMIN_PASSWORD (the admin account approves the demo agents).')
  process.exit(1)
}

async function call(method, path, { token, body, form } = {}) {
  const headers = { Accept: 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`
  if (body) headers['Content-Type'] = 'application/json'
  const response = await fetch(API + path, { method, headers, body: form ?? (body ? JSON.stringify(body) : undefined) })
  const text = await response.text()
  if (!response.ok) throw new Error(`${method} ${path} → ${response.status} ${text}`)
  return text ? JSON.parse(text) : null
}

const login = async (email, password) => (await call('POST', '/api/auth/login', { body: { email, password } })).accessToken

const accounts = {
  pierre: { fullName: 'Pierre Démo (agent)', phone: '+1 201 555 0101', agent: true },
  nadia: { fullName: 'Nadia Démo (agente)', phone: '+1 201 555 0102', agent: true },
  marie: { fullName: 'Marie Démo (cliente)', phone: '+1 201 555 0103' },
  jean: { fullName: 'Jean Démo (client)', phone: null },
}

/** A 640×400 PNG with a diagonal two-colour gradient: a stand-in photo, generated locally. */
function placeholderPng([r1, g1, b1], [r2, g2, b2]) {
  const width = 640
  const height = 400
  const rows = Buffer.alloc((width * 3 + 1) * height)
  for (let y = 0; y < height; y++) {
    const row = y * (width * 3 + 1)
    for (let x = 0; x < width; x++) {
      const t = (x / width + y / height) / 2
      rows[row + 1 + x * 3] = Math.round(r1 + (r2 - r1) * t)
      rows[row + 2 + x * 3] = Math.round(g1 + (g2 - g1) * t)
      rows[row + 3 + x * 3] = Math.round(b1 + (b2 - b1) * t)
    }
  }
  const crcTable = Array.from({ length: 256 }, (_, n) => {
    let c = n
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
    return c >>> 0
  })
  const crc = (buf) => {
    let c = 0xffffffff
    for (const byte of buf) c = crcTable[(c ^ byte) & 0xff] ^ (c >>> 8)
    return (c ^ 0xffffffff) >>> 0
  }
  const chunk = (type, data) => {
    const length = Buffer.alloc(4)
    length.writeUInt32BE(data.length)
    const typed = Buffer.concat([Buffer.from(type), data])
    const sum = Buffer.alloc(4)
    sum.writeUInt32BE(crc(typed))
    return Buffer.concat([length, typed, sum])
  }
  const header = Buffer.alloc(13)
  header.writeUInt32BE(width, 0)
  header.writeUInt32BE(height, 4)
  header[8] = 8 // bit depth
  header[9] = 2 // RGB
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(rows)),
    chunk('IEND', Buffer.alloc(0)),
  ])
}

const palettes = [
  [[11, 93, 107], [242, 165, 65]],
  [[26, 127, 75], [227, 241, 242]],
  [[154, 91, 0], [253, 241, 220]],
  [[19, 41, 45], [11, 93, 107]],
]

const listings = [
  { owner: 'pierre', key: 'petionville', title: '[Démo] Maison 3 chambres à Pétion-Ville', propertyType: 'House', transactionType: 'Rent', price: 1500, currency: 'USD', department: 'Ouest', city: 'Pétion-Ville', address: 'Rue de démonstration, Pétion-Ville', bedrooms: 3, bathrooms: 2, area: 180, status: 'Available', description: 'Maison lumineuse avec cour, citerne et génératrice. Quartier calme, proche des commerces. (Annonce de démonstration.)' },
  { owner: 'pierre', key: 'delmas', title: '[Démo] Appartement meublé à Delmas', propertyType: 'Apartment', transactionType: 'Rent', price: 75000, currency: 'HTG', department: 'Ouest', city: 'Delmas', address: 'Rue de démonstration, Delmas', bedrooms: 2, bathrooms: 1, area: 85, status: 'Available', description: 'Appartement meublé au 2e étage, parking et sécurité 24h/24. (Annonce de démonstration.)' },
  { owner: 'pierre', key: 'cap', title: '[Démo] Terrain à vendre à Cap-Haïtien', propertyType: 'Land', transactionType: 'Sale', price: 85000, currency: 'USD', department: 'Nord', city: 'Cap-Haïtien', address: 'Route de démonstration, Cap-Haïtien', bedrooms: null, bathrooms: null, area: 1200, status: 'Available', description: 'Terrain plat et clôturé, idéal pour une construction résidentielle. (Annonce de démonstration.)' },
  { owner: 'pierre', key: 'saintmarc', title: '[Démo] Local commercial à Saint-Marc', propertyType: 'CommercialSpace', transactionType: 'Rent', price: 900, currency: 'USD', department: 'Artibonite', city: 'Saint-Marc', address: 'Rue de démonstration, Saint-Marc', bedrooms: null, bathrooms: 1, area: 60, status: 'Draft', description: 'Local en rez-de-chaussée, bonne visibilité. Brouillon non publié. (Annonce de démonstration.)' },
  { owner: 'nadia', key: 'kenscoff', title: '[Démo] Villa avec vue à Kenscoff', propertyType: 'House', transactionType: 'Sale', price: 320000, currency: 'USD', department: 'Ouest', city: 'Kenscoff', address: 'Route de démonstration, Kenscoff', bedrooms: 4, bathrooms: 3, area: 320, status: 'Available', description: 'Villa en montagne avec jardin et vue dégagée, climat frais toute l’année. (Annonce de démonstration.)' },
  { owner: 'nadia', key: 'cayes', title: '[Démo] Bureau aux Cayes', propertyType: 'Office', transactionType: 'Rent', price: 45000, currency: 'HTG', department: 'Sud', city: 'Les Cayes', address: 'Rue de démonstration, Les Cayes', bedrooms: null, bathrooms: 1, area: 40, status: 'Available', description: 'Bureau climatisé, internet disponible, proche du centre-ville. (Annonce de démonstration.)' },
  { owner: 'nadia', key: 'jacmel', title: '[Démo] Maison à Jacmel (vendue)', propertyType: 'House', transactionType: 'Sale', price: 95000, currency: 'USD', department: 'Sud-Est', city: 'Jacmel', address: 'Rue de démonstration, Jacmel', bedrooms: 3, bathrooms: 2, area: 150, status: 'Sold', description: 'Maison de ville déjà vendue : elle n’apparaît pas dans la recherche publique. (Annonce de démonstration.)' },
]

// ---- Accounts ------------------------------------------------------------------

const adminToken = await login(ADMIN.email, ADMIN.password)
const tokens = {}
const ids = {}

for (const [key, account] of Object.entries(accounts)) {
  const email = `${key}@${DEMO_DOMAIN}`
  let user
  try {
    user = await call('POST', '/api/auth/register', {
      body: {
        email,
        password: PASSWORD,
        fullName: account.fullName,
        requestAgent: Boolean(account.agent),
        phoneNumber: account.phone ?? undefined,
        phonePublicConsent: Boolean(account.agent),
        agentRequestMessage: account.agent ? 'Compte de démonstration.' : undefined,
      },
    })
  } catch (error) {
    if (String(error).includes('409')) {
      console.error(`${email} already exists: run "node scripts/demo/cleanup.mjs" first.`)
      process.exit(1)
    }
    throw error
  }
  ids[key] = user.id
  if (account.agent) await call('POST', `/api/admin/agent-requests/${user.id}/approve`, { token: adminToken })
  tokens[key] = await login(email, PASSWORD)
  console.log(`account  ${email}${account.agent ? ' (approved agent)' : ''}`)
}

// ---- Listings --------------------------------------------------------------------

const property = {}
for (const [index, listing] of listings.entries()) {
  const { owner, key, ...body } = listing
  const created = await call('POST', '/api/properties', { token: tokens[owner], body: { ...body, country: 'Haïti', imageUrls: [] } })
  property[key] = created.id
  const photos = listing.status === 'Draft' ? 1 : 2
  for (let n = 0; n < photos; n++) {
    const [from, to] = palettes[(index + n) % palettes.length]
    const form = new FormData()
    form.append('file', new Blob([placeholderPng(from, to)], { type: 'image/png' }), `demo-${key}-${n + 1}.png`)
    await call('POST', `/api/properties/${created.id}/images`, { token: tokens[owner], form })
  }
  console.log(`listing  ${listing.title} (${listing.status})`)
}

// ---- Requests ----------------------------------------------------------------------

/** A date N days from now at the given hour in Haiti (UTC−5), as ISO with offset. */
function haitiDate(days, hour) {
  const date = new Date()
  date.setUTCDate(date.getUTCDate() + days)
  date.setUTCHours(hour + 5, 0, 0, 0)
  return date.toISOString()
}

const send = (client, key, body) => call('POST', `/api/properties/${property[key]}/requests`, { token: tokens[client], body })
const act = (who, request, verb, body = {}) => call('POST', `/api/requests/${request.id}/${verb}`, { token: tokens[who], body })

const rental = await send('marie', 'petionville', { type: 'Rental', message: 'Bonjour, je cherche une maison pour ma famille à partir de janvier.', shareClientPhone: true })
await act('pierre', rental, 'accept', { message: 'Avec plaisir. Appelez-moi pour signer le bail.' })

const visit = await send('marie', 'petionville', { type: 'Visit', visitAt: haitiDate(3, 10), message: 'Est-ce possible en matinée ?', shareClientPhone: true })
await act('pierre', visit, 'propose-date', { proposedVisitAt: haitiDate(4, 15), message: 'Je ne suis pas disponible ce jour-là, que diriez-vous du lendemain à 15 h ?' })

const purchase = await send('jean', 'cap', { type: 'Purchase', message: 'Le prix est-il négociable ? Je peux payer comptant.', shareClientPhone: false })
await act('pierre', purchase, 'respond', { message: 'Le prix est légèrement négociable. Souhaitez-vous visiter le terrain ?' })

await send('jean', 'delmas', { type: 'Rental', message: 'L’appartement est-il encore disponible pour décembre ?', shareClientPhone: false })
await send('marie', 'kenscoff', { type: 'Purchase', message: 'Je suis intéressée, pouvez-vous m’envoyer plus de photos ?', shareClientPhone: true })

const declined = await send('jean', 'cayes', { type: 'Visit', visitAt: haitiDate(2, 9), shareClientPhone: false })
await act('nadia', declined, 'decline', { message: 'Le bureau est en travaux cette semaine, revenez vers moi la semaine prochaine.' })

const cancelled = await send('marie', 'delmas', { type: 'Visit', visitAt: haitiDate(5, 11), shareClientPhone: false })
await act('marie', cancelled, 'cancel')

// Nadia withdraws her consent: her listings show no number (her approval stays).
await call('PUT', '/api/auth/me', {
  token: tokens.nadia,
  body: { fullName: accounts.nadia.fullName, phoneNumber: accounts.nadia.phone, phonePublicConsent: false },
})

console.log(`
Done. Password for every demo account: ${PASSWORD}
  pierre@${DEMO_DOMAIN}  approved agent, number shown (4 listings, requests to answer)
  nadia@${DEMO_DOMAIN}   approved agent, number hidden (consent withdrawn)
  marie@${DEMO_DOMAIN}   client with a phone (rental accepted, visit rescheduled, purchase pending)
  jean@${DEMO_DOMAIN}    client without a phone (purchase answered, rental pending, visit declined)
Remove everything with: node scripts/demo/cleanup.mjs`)
