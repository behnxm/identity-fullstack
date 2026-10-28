import { defineEventHandler } from 'h3'

export default defineEventHandler(async (event) => {
  const token = await getValidAccessToken(event)
  const session = await getUserSession(event)

  const policies = await apiFetch<Record<string, { allowed: boolean; reason: string | null }>>(
    event,
    '/api/v1/me/policies',
    {
      headers: {
        Authorization: `Bearer ${token}`
      }
    }
  )

  await setUserSession(event, {
    ...session,
    policies
  })

  return { policies }
})
