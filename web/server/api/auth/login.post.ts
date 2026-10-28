import { defineEventHandler, readBody } from 'h3'
import { z } from 'zod'

const LoginSchema = z.object({
  email: z.string().email().max(256),
  password: z.string().min(1).max(128),
  rememberMe: z.boolean().optional()
})

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const validation = LoginSchema.safeParse(body)
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

  const apiResponse = await apiFetch<AuthApiResponse>(event, '/api/v1/auth/login', {
    method: 'POST',
    body: validation.data
  })

  return await applyAuthResponse(event, apiResponse)
})
