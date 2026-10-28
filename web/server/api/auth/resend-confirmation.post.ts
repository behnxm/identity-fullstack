import { defineEventHandler, readBody } from 'h3'
import { z } from 'zod'

const ResendConfirmationSchema = z.object({
  email: z.string().email().max(256)
})

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const validation = ResendConfirmationSchema.safeParse(body)
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

  await apiFetch(event, '/api/v1/auth/resend-confirmation', {
    method: 'POST',
    body: validation.data
  })

  return { status: 'accepted' }
})
