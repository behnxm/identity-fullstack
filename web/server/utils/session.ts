import type { H3Event } from 'h3'

export interface AuthApiResponse {
  status: string
  accessToken?: string
  expiresIn?: number
  refreshToken?: string
  refreshExpiresAt?: string
  sessionId?: string
  challengeToken?: string
  user?: {
    id: string
    email: string
    displayName: string
    emailConfirmed: boolean
    twoFactorEnabled: boolean
    roles: string[]
    amr?: string[]
  }
}

export function resolveLandingPath(policies: Record<string, { allowed: boolean; reason: string | null }>): string {
  if (policies['panel.admin']?.allowed) {
    return '/admin'
  }
  if (policies['panel.admin']?.reason === 'mfa_required') {
    return '/app/security'
  }
  if (policies['panel.user']?.allowed) {
    return '/app'
  }
  return '/403'
}

export async function applyAuthResponse(event: H3Event, response: AuthApiResponse) {
  if (response.status === 'mfa_required' || response.status === 'two_factor_required' || !response.accessToken) {
    await setUserSession(event, {
      challengeToken: response.challengeToken
    })
    return {
      status: 'mfa_required',
      challengeToken: response.challengeToken,
      mfaToken: response.challengeToken
    }
  }

  const now = Math.floor(Date.now() / 1000)
  const expiresAt = now + (response.expiresIn || 600)
  const refreshExpiresAt = response.refreshExpiresAt
    ? Math.floor(new Date(response.refreshExpiresAt).getTime() / 1000)
    : now + 86400 * 7

  // Fetch evaluated policies from API single source of truth
  const policies = await apiFetch<Record<string, { allowed: boolean; reason: string | null }>>(
    event,
    '/api/v1/me/policies',
    {
      headers: {
        Authorization: `Bearer ${response.accessToken}`
      }
    }
  )

  const landingPath = resolveLandingPath(policies)

  await setUserSession(event, {
    user: response.user,
    tokens: {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken!,
      expiresAt,
      refreshExpiresAt,
      sessionId: response.sessionId!
    },
    policies
  })

  return {
    status: 'authenticated',
    user: response.user,
    policies,
    landingPath
  }
}
