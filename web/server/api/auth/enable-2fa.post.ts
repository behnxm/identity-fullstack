import { defineEventHandler, readBody } from 'h3'
import { z } from 'zod'

const schema = z.object({
  code: z.string().min(6).max(7)
})

interface EnableTwoFactorApiResponse {
  recoveryCodes: string[]
  accessToken: string
  expiresIn: number
  refreshToken: string
  refreshExpiresAt: string
}

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const parsed = schema.safeParse(body)
  if (!parsed.success) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Bad Request',
      data: {
        code: 'auth.invalid_code',
        title: 'Validation Error',
        detail: 'A valid 6-digit verification code is required.'
      }
    })
  }

  const token = await getValidAccessToken(event)
  const currentSession = (await getUserSession(event)) as any

  const apiRes = await apiFetch<EnableTwoFactorApiResponse>(
    event,
    '/api/v1/me/security/2fa/enable',
    {
      method: 'POST',
      body: parsed.data,
      headers: {
        Authorization: `Bearer ${token}`
      }
    }
  )

  const now = Math.floor(Date.now() / 1000)
  const expiresAt = now + (apiRes.expiresIn || 600)
  const refreshExpiresAt = apiRes.refreshExpiresAt
    ? Math.floor(new Date(apiRes.refreshExpiresAt).getTime() / 1000)
    : now + 86400 * 7

  // Refresh policies with new stepped-up token
  const policies = await apiFetch<Record<string, { allowed: boolean; reason: string | null }>>(
    event,
    '/api/v1/me/policies',
    {
      headers: {
        Authorization: `Bearer ${apiRes.accessToken}`
      }
    }
  )

  const user = currentSession?.user ? { ...currentSession.user } : undefined
  if (user) {
    user.twoFactorEnabled = true
    const currentAmr: string[] = Array.isArray(user.amr) ? user.amr : []
    if (!currentAmr.includes('mfa')) {
      user.amr = [...currentAmr, 'mfa']
    }
  }

  await setUserSession(event, {
    user,
    tokens: {
      accessToken: apiRes.accessToken,
      refreshToken: apiRes.refreshToken,
      expiresAt,
      refreshExpiresAt,
      sessionId: currentSession?.tokens?.sessionId || ''
    },
    policies
  })

  // Return ONLY recovery codes to browser - token stays sealed in session cookie
  return {
    recoveryCodes: apiRes.recoveryCodes
  }
})
