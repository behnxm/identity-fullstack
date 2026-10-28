import { defineEventHandler } from 'h3'

export default defineEventHandler(async (event) => {
  const session = (await getUserSession(event)) as any
  if (!session?.tokens?.accessToken || !session?.user) {
    return {
      authenticated: false,
      user: null,
      policies: null
    }
  }

  return {
    authenticated: true,
    user: session.user,
    policies: session.policies || {}
  }
})
