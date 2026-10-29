import { defineEventHandler, readBody } from 'h3'
import { z } from 'zod'

const ConfirmEmailSchema = z.object({
  userId: z.string().uuid(),
  token: z.string().min(1).transform(t => t.trim().replace(/ /g, '+'))
})

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const validation = ConfirmEmailSchema.safeParse(body)
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

  await apiFetch(event, '/api/v1/auth/confirm-email', {
    method: 'POST',
    body: validation.data
  })

  return { status: 'confirmed' }
})
