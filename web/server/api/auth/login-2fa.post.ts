import { defineEventHandler, readBody } from 'h3'
import { z } from 'zod'

const TwoFactorLoginSchema = z.object({
  challengeToken: z.string().optional(),
  mfaToken: z.string().optional(),
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

  const session = await getUserSession(event)
  const challengeToken = validation.data.challengeToken || validation.data.mfaToken || (session as any)?.challengeToken

  if (!challengeToken) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Bad Request',
      data: {
        code: 'auth.challenge_expired',
        title: 'Challenge Expired',
        detail: 'The two-factor authentication challenge has expired or is invalid. Please sign in again.'
      }
    })
  }

  const apiResponse = await apiFetch<AuthApiResponse>(event, '/api/v1/auth/login/2fa', {
    method: 'POST',
    body: {
      challengeToken,
      code: validation.data.code,
      recoveryCode: validation.data.recoveryCode
    }
  })

  return await applyAuthResponse(event, apiResponse)
})
