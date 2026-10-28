declare module '#auth-utils' {
  interface User {
    id: string
    email: string
    displayName: string
    emailConfirmed: boolean
    twoFactorEnabled: boolean
    roles: string[]
  }

  interface UserSession {
    user?: User
    tokens?: {
      accessToken: string
      refreshToken: string
      expiresAt: number // unix epoch seconds
      refreshExpiresAt: number // unix epoch seconds
      sessionId: string
    }
    policies?: Record<string, { allowed: boolean; reason: string | null }>
    challengeToken?: string
  }
}

export {}
