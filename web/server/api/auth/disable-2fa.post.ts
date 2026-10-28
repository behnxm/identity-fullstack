import { defineEventHandler, readBody } from 'h3'
import { z } from 'zod'

const schema = z.object({
  password: z.string().min(1),
  code: z.string().min(6).max(7)
})

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const parsed = schema.safeParse(body)
  if (!parsed.success) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Bad Request',
      data: {
        code: 'auth.invalid_payload',
        title: 'Validation Error',
        detail: 'Current password and verification code are required.'
      }
    })
  }

  const token = await getValidAccessToken(event)
  const currentSession = (await getUserSession(event)) as any

  await apiFetch(
    event,
    '/api/v1/me/security/2fa/disable',
    {
      method: 'POST',
      body: parsed.data,
      headers: {
        Authorization: `Bearer ${token}`
      }
    }
  )

  // Force token refresh by setting expiration to 0 so next request gets non-MFA token
  if (currentSession?.tokens) {
    currentSession.tokens.expiresAt = 0
  }

  // Refresh policies
  const newToken = await getValidAccessToken(event)
  const policies = await apiFetch<Record<string, { allowed: boolean; reason: string | null }>>(
    event,
    '/api/v1/me/policies',
    {
      headers: {
        Authorization: `Bearer ${newToken}`
      }
    }
  )

  const user = currentSession?.user ? { ...currentSession.user } : undefined
  if (user) {
    user.twoFactorEnabled = false
    const currentAmr: string[] = Array.isArray(user.amr) ? user.amr : []
    user.amr = currentAmr.filter(a => a !== 'mfa')
  }

  await setUserSession(event, {
    user,
    tokens: currentSession?.tokens,
    policies
  })

  return { success: true }
})
