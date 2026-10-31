// @vitest-environment jsdom
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AgentAccount } from '../api/types'
import { Header } from '../components/Header'
import { authState, json, makeUser, mockFetch, renderWithAuth } from '../test/render'
import { AccountPage } from './AccountPage'
import { AdminPage } from './AdminPage'
import { RegisterPage } from './AuthPages'
import { DashboardPage } from './DashboardPage'

const emptyPage = { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 }

describe('Registration', () => {
  it('creates a client by default, with an optional phone', async () => {
    const register = vi.fn().mockResolvedValue(makeUser())
    renderWithAuth(<RegisterPage />, authState(null, { register }))

    expect((screen.getByLabelText('Téléphone (facultatif)') as HTMLInputElement).required).toBe(false)
    expect(screen.queryByText(/J’accepte que mon numéro/)).toBeNull()

    await userEvent.type(screen.getByLabelText('Nom complet'), 'Marie Joseph')
    await userEvent.type(screen.getByLabelText('Adresse e-mail'), 'marie@imotek.test')
    await userEvent.type(screen.getByLabelText('Mot de passe'), 'Passw0rd-123')
    await userEvent.click(screen.getByRole('button', { name: 'Créer mon compte' }))

    expect(register).toHaveBeenCalledWith(expect.objectContaining({ requestAgent: false, phonePublicConsent: false }))
  })

  it('files an agent request with a required phone and the display consent', async () => {
    const register = vi.fn().mockResolvedValue(makeUser({ agentStatus: 'Pending' }))
    renderWithAuth(<RegisterPage />, authState(null, { register }))

    await userEvent.click(screen.getByLabelText('Publier des annonces'))

    expect(screen.getByText(/après validation de votre demande par un administrateur/)).toBeTruthy()
    expect((screen.getByLabelText('Téléphone') as HTMLInputElement).required).toBe(true)

    await userEvent.type(screen.getByLabelText('Nom complet'), 'Jean Agent')
    await userEvent.type(screen.getByLabelText('Adresse e-mail'), 'jean@imotek.test')
    await userEvent.type(screen.getByLabelText('Mot de passe'), 'Passw0rd-123')
    await userEvent.type(screen.getByLabelText('Téléphone'), '+509 3712 3456')
    await userEvent.click(screen.getByLabelText(/J’accepte que mon numéro/))
    await userEvent.click(screen.getByRole('button', { name: 'Créer mon compte et envoyer ma demande' }))

    expect(register).toHaveBeenCalledWith(
      expect.objectContaining({ requestAgent: true, phoneNumber: '+509 3712 3456', phonePublicConsent: true }),
    )
  })

  it('shows the API validation messages in French next to the fields', async () => {
    const { ApiError } = await import('../api/client')
    const register = vi.fn().mockRejectedValue(
      new ApiError(400, 'Certaines informations sont invalides.', {
        phoneNumber: ['Numéro de téléphone invalide pour ce pays. Exemple : +509 3712 3456.'],
      }),
    )
    renderWithAuth(<RegisterPage />, authState(null, { register }))

    await userEvent.click(screen.getByLabelText('Publier des annonces'))
    await userEvent.click(screen.getByRole('button', { name: 'Créer mon compte et envoyer ma demande' }))

    expect(await screen.findByText(/Numéro de téléphone invalide pour ce pays/)).toBeTruthy()
  })
})

describe('Account page', () => {
  it('offers the agent request form to a client', () => {
    renderWithAuth(<AccountPage />, authState(makeUser()))

    expect(screen.getByTestId('agent-status').textContent).toContain('pas encore demandé')
    expect(screen.getByRole('button', { name: 'Demander à devenir agent' })).toBeTruthy()
  })

  it('shows a pending request without a second request form', () => {
    renderWithAuth(<AccountPage />, authState(makeUser({ agentStatus: 'Pending', phoneNumber: '+50937123456' })))

    expect(screen.getByTestId('agent-status').textContent).toContain('en cours d’examen')
    expect(screen.queryByRole('button', { name: /Demander à devenir agent|nouvelle demande/ })).toBeNull()
  })

  it('shows the rejection reason and allows a new request', () => {
    renderWithAuth(
      <AccountPage />,
      authState(makeUser({ agentStatus: 'Rejected', agentRejectionReason: 'Documents manquants' })),
    )

    expect(screen.getByText('Motif : Documents manquants')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Envoyer une nouvelle demande' })).toBeTruthy()
  })

  it('warns legacy agents that they cannot publish yet', () => {
    renderWithAuth(<AccountPage />, authState(makeUser({ roles: ['Agent'], isLegacyAgent: true })))

    expect(screen.getByText(/Compte agent existant en attente de validation/)).toBeTruthy()
  })

  it('sends the agent request and updates the cached user', async () => {
    const updated = makeUser({ agentStatus: 'Pending', phoneNumber: '+50937123456' })
    const calls = mockFetch(() => json(200, updated))
    const updateUser = vi.fn()
    renderWithAuth(<AccountPage />, authState(makeUser(), { updateUser }))

    await userEvent.type(screen.getAllByLabelText('Téléphone')[0], '3712 3456')
    await userEvent.click(screen.getAllByLabelText(/J’accepte que mon numéro/)[0])
    await userEvent.click(screen.getByRole('button', { name: 'Demander à devenir agent' }))

    await waitFor(() => expect(updateUser).toHaveBeenCalledWith(updated))
    expect(calls[0].url).toBe('/api/auth/agent-request')
    expect(JSON.parse(String(calls[0].init.body))).toEqual({ phoneNumber: '3712 3456', phonePublicConsent: true, message: '' })
  })
})

describe('Agent dashboard', () => {
  it('hides publishing for an agent that is not approved', async () => {
    mockFetch(() => json(200, emptyPage))
    renderWithAuth(<DashboardPage />, authState(makeUser({ roles: ['Agent'], isLegacyAgent: true })))

    expect((await screen.findByTestId('approval-required')).textContent).toContain('Compte agent existant')
    expect(screen.queryByRole('link', { name: /Ajouter un bien/ })).toBeNull()
  })

  it('lets an approved agent add a listing', async () => {
    mockFetch(() => json(200, emptyPage))
    renderWithAuth(
      <DashboardPage />,
      authState(makeUser({ roles: ['Client', 'Agent'], agentStatus: 'Approved', canPublish: true })),
    )

    expect(await screen.findByRole('link', { name: '+ Ajouter un bien' })).toBeTruthy()
    expect(screen.queryByTestId('approval-required')).toBeNull()
  })
})

describe('Administration — agent requests', () => {
  const pending: AgentAccount = {
    id: 'a1',
    email: 'jean@imotek.test',
    fullName: 'Jean Agent',
    roles: ['Client'],
    phoneNumber: '+50937123456',
    agentStatus: 'Pending',
    agentRequestMessage: 'Agence à Delmas',
    agentRequestedAt: '2026-10-30T10:00:00Z',
    agentReviewedAt: null,
    agentRejectionReason: null,
    isLegacyAgent: false,
  }

  it('lists pending requests and approves one', async () => {
    const calls = mockFetch((url, init) =>
      init.method === 'POST' ? json(200, { ...pending, agentStatus: 'Approved' }) : json(200, url.includes('Pending') ? [pending] : []),
    )
    renderWithAuth(<AdminPage />, authState(makeUser({ roles: ['Admin'] })))

    expect(await screen.findByText('Jean Agent')).toBeTruthy()
    expect(screen.getByText('« Agence à Delmas »')).toBeTruthy()

    await userEvent.click(screen.getByRole('button', { name: 'Valider' }))

    await waitFor(() => expect(calls.some((c) => c.url === '/api/admin/agent-requests/a1/approve')).toBe(true))
  })

  it('requires a reason before rejecting', async () => {
    const calls = mockFetch((_, init) => (init.method === 'POST' ? json(200, { ...pending, agentStatus: 'Rejected' }) : json(200, [pending])))
    renderWithAuth(<AdminPage />, authState(makeUser({ roles: ['Admin'] })))

    await userEvent.click(await screen.findByRole('button', { name: 'Refuser' }))
    const confirm = screen.getByRole('button', { name: 'Confirmer le refus' })
    expect((confirm as HTMLButtonElement).disabled).toBe(true)

    await userEvent.type(screen.getByLabelText('Motif du refus'), 'Pièces manquantes')
    await userEvent.click(confirm)

    await waitFor(() => {
      const reject = calls.find((c) => c.url.endsWith('/reject'))
      expect(reject && JSON.parse(String(reject.init.body))).toEqual({ reason: 'Pièces manquantes' })
    })
  })

  it('labels legacy agents that need approval', async () => {
    mockFetch(() => json(200, [{ ...pending, agentStatus: 'None', roles: ['Agent'], isLegacyAgent: true }]))
    renderWithAuth(<AdminPage />, authState(makeUser({ roles: ['Admin'] })))

    await userEvent.click(screen.getByRole('tab', { name: 'Agents existants à valider' }))

    expect(await screen.findByText('Legacy Agent – Approval Required')).toBeTruthy()
  })
})

describe('Header links by role', () => {
  it.each([
    [['Client'], false, false],
    [['Client', 'Agent'], true, false],
    [['Admin'], false, true],
  ] as const)('roles %j: agent area %s, admin %s', (roles, agent, admin) => {
    renderWithAuth(<Header />, authState(makeUser({ roles: [...roles] })))

    expect(screen.queryByRole('link', { name: 'Espace agent' }) !== null).toBe(agent)
    expect(screen.queryByRole('link', { name: 'Administration' }) !== null).toBe(admin)
    expect(screen.getByRole('link', { name: 'Marie Joseph' }).getAttribute('href')).toBe('/compte')
  })
})

describe('Redirect after sign-in', () => {
  it('never sends a user to a page recorded for someone else', async () => {
    const { canOpen } = await import('../auth/redirects')
    const client = makeUser()
    const agent = makeUser({ roles: ['Client', 'Agent'] })
    const admin = makeUser({ roles: ['Admin'] })

    expect(canOpen('/admin', agent)).toBe(false)
    expect(canOpen('/espace-agent', admin)).toBe(false)
    expect(canOpen('/espace-agent/biens/nouveau', client)).toBe(false)
    expect(canOpen('/admin', admin)).toBe(true)
    expect(canOpen('/espace-agent', agent)).toBe(true)
    expect(canOpen('/compte', client)).toBe(true)
  })
})
