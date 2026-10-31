// @vitest-environment jsdom
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { contactLinks, type ClientRequest } from '../api/requests'
import type { Property } from '../api/types'
import { Header } from '../components/Header'
import { PropertyActions } from '../components/PropertyActions'
import { RequestList } from '../components/RequestList'
import { authState, json, makeUser, mockFetch, renderWithAuth } from '../test/render'
import { DashboardPage } from './DashboardPage'

function makeProperty(overrides: Partial<Property> = {}): Property {
  return {
    id: 'p1',
    title: 'Maison à Pétion-Ville',
    description: 'Belle maison',
    propertyType: 'House',
    transactionType: 'Rent',
    price: 1500,
    currency: 'USD',
    country: 'Haïti',
    department: 'Ouest',
    city: 'Pétion-Ville',
    address: '12, rue Exemple',
    bedrooms: 3,
    bathrooms: 2,
    area: 120,
    imageUrls: [],
    photos: [],
    externalImageUrls: [],
    status: 'Available',
    ownerId: 'agent-1',
    createdAt: '2026-10-30T12:00:00Z',
    updatedAt: '2026-10-30T12:00:00Z',
    ...overrides,
  }
}

function makeRequest(overrides: Partial<ClientRequest> = {}): ClientRequest {
  return {
    id: 'r1',
    type: 'Rental',
    status: 'Pending',
    propertyId: 'p1',
    propertyTitle: 'Maison à Pétion-Ville',
    propertyCity: 'Pétion-Ville',
    message: 'Je cherche pour janvier.',
    shareClientPhone: false,
    agentResponse: null,
    visitAt: null,
    proposedVisitAt: null,
    createdAt: '2026-10-31T12:00:00Z',
    updatedAt: '2026-10-31T12:00:00Z',
    client: null,
    agent: null,
    allowedActions: [],
    ...overrides,
  }
}

const buttonNames = () => screen.queryAllByRole('button').map((b) => b.textContent)
const contact = (phoneNumber: string | null) => json(200, { agentName: 'Jean Agent', phoneNumber })

describe('Property actions', () => {
  it('asks visitors to sign in and shows no request or contact', () => {
    const calls = mockFetch(() => contact('+50937123456'))
    renderWithAuth(<PropertyActions property={makeProperty()} />, authState(null))

    expect(screen.getByRole('link', { name: 'Se connecter' }).getAttribute('href')).toBe('/connexion')
    expect(screen.getByRole('link', { name: 'Créer un compte' })).toBeTruthy()
    expect(buttonNames()).toEqual([])
    expect(calls).toHaveLength(0)
  })

  it('offers rental and visit on a property for rent, never purchase', async () => {
    mockFetch(() => contact(null))
    renderWithAuth(<PropertyActions property={makeProperty()} />, authState(makeUser()))

    await screen.findByText('Jean Agent')
    expect(buttonNames()).toEqual(['Demander une location', 'Demander une visite'])
  })

  it('offers purchase and visit on a property for sale, never rental', async () => {
    mockFetch(() => contact(null))
    renderWithAuth(<PropertyActions property={makeProperty({ transactionType: 'Sale' })} />, authState(makeUser()))

    await screen.findByText('Jean Agent')
    expect(buttonNames()).toEqual(['Demander à acheter', 'Demander une visite'])
  })

  it('shows nothing on an unavailable property or to its owner', () => {
    mockFetch(() => contact(null))
    const { container, unmount } = renderWithAuth(
      <PropertyActions property={makeProperty({ status: 'Rented' })} />,
      authState(makeUser()),
    )
    expect(container.textContent).toBe('')
    unmount()

    const owner = makeUser({ id: 'agent-1', roles: ['Client', 'Agent'], canPublish: true })
    const second = renderWithAuth(<PropertyActions property={makeProperty()} />, authState(owner))
    expect(second.container.textContent).toBe('')
  })

  it('shows call, WhatsApp and SMS links when the agent shares a number', async () => {
    mockFetch(() => contact('+50937123456'))
    renderWithAuth(<PropertyActions property={makeProperty()} />, authState(makeUser()))

    expect((await screen.findByRole('link', { name: 'Appeler' })).getAttribute('href')).toBe('tel:+50937123456')
    expect(screen.getByRole('link', { name: 'WhatsApp' }).getAttribute('href')).toBe('https://wa.me/50937123456')
    expect(screen.getByRole('link', { name: 'SMS' }).getAttribute('href')).toBe('sms:+50937123456')
  })

  it('shows no contact button when the agent does not share a number', async () => {
    mockFetch(() => contact(null))
    renderWithAuth(<PropertyActions property={makeProperty()} />, authState(makeUser()))

    expect(await screen.findByText(/n’affiche pas son numéro/)).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Appeler' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'WhatsApp' })).toBeNull()
  })

  it('accounts without the Client role see the contact but cannot send requests', async () => {
    mockFetch(() => contact(null))
    renderWithAuth(<PropertyActions property={makeProperty()} />, authState(makeUser({ roles: ['Admin'] })))

    expect(await screen.findByText(/réservées aux comptes clients/)).toBeTruthy()
    expect(buttonNames()).toEqual([])
  })

  it('sends a visit request with the date, the message and the phone consent', async () => {
    const calls = mockFetch((url) => (url.endsWith('/contact') ? contact(null) : json(201, makeRequest({ type: 'Visit' }))))
    renderWithAuth(<PropertyActions property={makeProperty()} />, authState(makeUser({ phoneNumber: '+50937000002' })))

    await userEvent.click(await screen.findByRole('button', { name: 'Demander une visite' }))
    await userEvent.type(screen.getByLabelText('Date et heure souhaitées'), '2030-01-15T10:00')
    await userEvent.type(screen.getByLabelText('Message (facultatif)'), 'Samedi matin ?')
    await userEvent.click(screen.getByLabelText(/Partager mon numéro/))
    await userEvent.click(screen.getByRole('button', { name: 'Envoyer la demande' }))

    expect(await screen.findByText(/Votre demande a été envoyée/)).toBeTruthy()
    const sent = calls.find((c) => c.url === '/api/properties/p1/requests')!
    const body = JSON.parse(String(sent.init.body))
    expect(body).toMatchObject({ type: 'Visit', message: 'Samedi matin ?', shareClientPhone: true })
    expect(body.visitAt).toBe(new Date('2030-01-15T10:00').toISOString())
  })

  it('cannot share a phone that is not on the profile', async () => {
    mockFetch(() => contact(null))
    renderWithAuth(<PropertyActions property={makeProperty()} />, authState(makeUser({ phoneNumber: null })))

    await userEvent.click(await screen.findByRole('button', { name: 'Demander une location' }))

    expect((screen.getByRole('checkbox') as HTMLInputElement).disabled).toBe(true)
    expect(screen.getByRole('link', { name: 'Mon compte' })).toBeTruthy()
  })
})

describe('Request lists', () => {
  it('shows the agent the client, the shared phone and only the allowed actions', async () => {
    const calls = mockFetch(() => json(200, makeRequest({ status: 'Accepted' })))
    const onChanged = vi.fn()
    const request = makeRequest({
      client: { fullName: 'Marie Joseph', email: 'marie@imotek.test', phoneNumber: '+50937000002' },
      shareClientPhone: true,
      allowedActions: ['Respond', 'Accept', 'Decline'],
    })
    renderWithAuth(<RequestList requests={[request]} viewer="agent" onChanged={onChanged} />, authState(makeUser()))

    expect(screen.getByRole('link', { name: 'marie@imotek.test' }).getAttribute('href')).toBe('mailto:marie@imotek.test')
    expect(screen.getByRole('link', { name: 'WhatsApp' }).getAttribute('href')).toBe('https://wa.me/50937000002')
    expect(screen.queryByRole('button', { name: 'Clôturer' })).toBeNull()

    await userEvent.type(screen.getByLabelText('Message au client (facultatif)'), 'Bienvenue')
    await userEvent.click(screen.getByRole('button', { name: 'Accepter' }))

    await waitFor(() => expect(onChanged).toHaveBeenCalled())
    const accept = calls.find((c) => c.url === '/api/requests/r1/accept')!
    expect(JSON.parse(String(accept.init.body))).toEqual({ message: 'Bienvenue' })
  })

  it('tells the agent when the client did not share a phone', () => {
    const request = makeRequest({ client: { fullName: 'Marie Joseph', email: 'marie@imotek.test', phoneNumber: null } })
    renderWithAuth(<RequestList requests={[request]} viewer="agent" onChanged={vi.fn()} />, authState(makeUser()))

    expect(screen.getByText(/n’a pas partagé son numéro/)).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Appeler' })).toBeNull()
  })

  it('lets the client accept a new visit date and filters by kind', async () => {
    const calls = mockFetch(() => json(200, makeRequest()))
    const requests = [
      makeRequest({
        id: 'v1',
        type: 'Visit',
        status: 'Rescheduled',
        visitAt: '2030-01-15T15:00:00Z',
        proposedVisitAt: '2030-01-16T15:00:00Z',
        agent: { fullName: 'Jean Agent', email: null, phoneNumber: null },
        allowedActions: ['Cancel', 'AcceptProposal'],
      }),
      makeRequest({ id: 'r2', type: 'Purchase', status: 'Declined', agentResponse: 'Déjà vendu.' }),
    ]
    renderWithAuth(<RequestList requests={requests} viewer="client" onChanged={vi.fn()} />, authState(makeUser()))

    expect(screen.getByText('Nouvelle date proposée', { selector: '.status' })).toBeTruthy()
    expect(screen.getByText('Déjà vendu.')).toBeTruthy()
    await userEvent.click(screen.getByRole('button', { name: 'Accepter la nouvelle date' }))
    await waitFor(() => expect(calls.some((c) => c.url === '/api/requests/v1/accept-proposal')).toBe(true))

    await userEvent.click(screen.getByRole('tab', { name: 'Achat (1)' }))
    expect(screen.getAllByTestId('request-item')).toHaveLength(1)
    expect(screen.queryByRole('button', { name: 'Accepter la nouvelle date' })).toBeNull()
  })
})

describe('Agent area and header', () => {
  it('archives a listing instead of deleting it, and an archived listing has no edit button', async () => {
    const page = (status: Property['status']) => ({
      items: [makeProperty({ status, ownerId: 'u1' })],
      page: 1,
      pageSize: 50,
      totalCount: 1,
      totalPages: 1,
    })
    let status: Property['status'] = 'Available'
    const calls = mockFetch((_url, init) => {
      if (init.method === 'DELETE') {
        status = 'Archived'
        return json(204)
      }
      return json(200, page(status))
    })
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const agent = makeUser({ roles: ['Client', 'Agent'], canPublish: true, agentStatus: 'Approved' })
    renderWithAuth(<DashboardPage />, authState(agent))

    await userEvent.click(await screen.findByRole('button', { name: 'Archiver' }))

    expect(await screen.findByText('Archivé', { selector: '.manage-item__body .status' })).toBeTruthy()
    expect(calls.some((c) => c.init.method === 'DELETE' && c.url === '/api/properties/p1')).toBe(true)
    expect(screen.queryByRole('link', { name: 'Modifier' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Archiver' })).toBeNull()
    expect(screen.getByRole('link', { name: 'Demandes reçues' }).getAttribute('href')).toBe('/espace-agent/demandes')
  })

  it('shows the unread notification count', async () => {
    mockFetch(() => json(200, { items: [], unreadCount: 3 }))
    renderWithAuth(<Header />, authState(makeUser()))

    expect((await screen.findByLabelText('3 non lues')).textContent).toBe('3')
    expect(screen.getByRole('link', { name: 'Mes demandes' })).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Demandes reçues' })).toBeNull()
  })
})

describe('contactLinks', () => {
  it('builds tel, wa.me and sms links from an E.164 number', () => {
    expect(contactLinks('+1 201 555 0123')).toEqual({
      call: 'tel:+12015550123',
      whatsApp: 'https://wa.me/12015550123',
      sms: 'sms:+12015550123',
    })
  })
})
