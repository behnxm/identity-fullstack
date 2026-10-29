import { defineEventHandler, readBody } from 'h3'
import { z } from 'zod'

const ResetPasswordSchema = z.object({
  userId: z.string().uuid(),
  token: z.string().min(1).transform(t => t.trim().replace(/ /g, '+')),
  newPassword: z.string().min(12, 'Password must be at least 12 characters').max(128)
})

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const validation = ResetPasswordSchema.safeParse(body)
  if (!validation.success) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Bad Request',
      data: {
        code: 'validation.failed',
        title: 'Validation Error',
        errors: validation.error.flatten().fieldErrors
      }
    })
  }

  await apiFetch(event, '/api/v1/auth/reset-password', {
    method: 'POST',
    body: validation.data
  })

  // Clear session to ensure clean state
  await clearUserSession(event)

  return { status: 'password_reset' }
})
