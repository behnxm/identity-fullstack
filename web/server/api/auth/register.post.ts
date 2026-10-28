import { defineEventHandler, readBody } from 'h3'
import { z } from 'zod'

const RegisterSchema = z.object({
  email: z.string().email().max(256),
  password: z.string().min(12, 'Password must be at least 12 characters').max(128),
  displayName: z.string().min(2).max(100)
})

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const validation = RegisterSchema.safeParse(body)
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

  await apiFetch(event, '/api/v1/auth/register', {
    method: 'POST',
    body: validation.data
  })

  return { status: 'accepted' }
})
