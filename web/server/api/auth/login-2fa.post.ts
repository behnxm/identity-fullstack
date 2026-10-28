import { defineEventHandler, readBody } from 'h3'
import { z } from 'zod'

const TwoFactorLoginSchema = z.object({
  challengeToken: z.string().min(1),
  code: z.string().optional(),
  recoveryCode: z.string().optional()
}).refine(data => !!data.code || !!data.recoveryCode, {
  message: 'Either TOTP code or recovery code must be provided.'
})

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const validation = TwoFactorLoginSchema.safeParse(body)
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

  const apiResponse = await apiFetch<AuthApiResponse>(event, '/api/v1/auth/login/2fa', {
    method: 'POST',
    body: validation.data
  })

  return await applyAuthResponse(event, apiResponse)
})
