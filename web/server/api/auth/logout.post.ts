import { defineEventHandler } from 'h3'

export default defineEventHandler(async (event) => {
  const session = (await getUserSession(event)) as any
  if (session?.tokens?.accessToken) {
    try {
      await apiFetch(event, '/api/v1/auth/logout', {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${session.tokens.accessToken}`
        }
      })
    } catch {
      // Continue clearing session even if API call fails
    }
  }

  await clearUserSession(event)
  return { status: 'logged_out' }
})
