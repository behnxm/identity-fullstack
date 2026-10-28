import type { H3Event } from 'h3'

// Single-flight in-flight refresh promise map keyed by sessionId
const refreshFlights = new Map<string, Promise<string>>()

export async function getValidAccessToken(event: H3Event): Promise<string> {
  const session = (await getUserSession(event)) as any
  if (!session?.tokens?.accessToken) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized',
      data: {
        code: 'auth.unauthenticated',
        title: 'Unauthenticated',
        detail: 'No active session or access token found.'
      }
    })
  }

  const { accessToken, refreshToken, expiresAt, sessionId } = session.tokens
  const now = Math.floor(Date.now() / 1000)

  // Proactive refresh: if more than 60 seconds remain, return current token
  if (expiresAt - now > 60) {
    return accessToken
  }

  // If a refresh is already in flight for this session, wait for it
  if (refreshFlights.has(sessionId)) {
    return await refreshFlights.get(sessionId)!
  }

  // Create single flight refresh promise
  const refreshPromise = (async () => {
    try {
      const response = await apiFetch<{
        accessToken: string
        refreshToken: string
        expiresIn: number
        refreshExpiresAt: string
      }>(event, '/api/v1/auth/refresh', {
        method: 'POST',
        body: { refreshToken }
      })

      const newExpiresAt = Math.floor(Date.now() / 1000) + response.expiresIn
      const newRefreshExpiresAt = Math.floor(new Date(response.refreshExpiresAt).getTime() / 1000)

      // Update session with new token pair
      await setUserSession(event, {
        ...session,
        tokens: {
          accessToken: response.accessToken,
          refreshToken: response.refreshToken,
          expiresAt: newExpiresAt,
          refreshExpiresAt: newRefreshExpiresAt,
          sessionId
        }
      })

      return response.accessToken
    } catch (err: any) {
      // Clear session if refresh token was revoked, expired, or reused
      await clearUserSession(event)
      throw createError({
        statusCode: 401,
        statusMessage: 'Unauthorized',
        data: {
          code: err.data?.extensions?.code || 'auth.unauthenticated',
          title: 'Session Expired',
          detail: 'Your session has expired. Please log in again.'
        }
      })
    } finally {
      refreshFlights.delete(sessionId)
    }
  })()

  refreshFlights.set(sessionId, refreshPromise)
  return await refreshPromise
}
