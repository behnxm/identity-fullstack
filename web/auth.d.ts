declare module '#auth-utils' {
  interface User {
    id: string
    email: string
    displayName: string
    emailConfirmed: boolean
    twoFactorEnabled: boolean
    roles: string[]
    amr?: string[]
  }

  interface SecureSessionData {
    tokens?: {
      accessToken: string
      refreshToken: string
      expiresAt: number
      refreshExpiresAt: number
      sessionId: string
    }
  }

  interface UserSession {
    user?: User
    tokens?: {
      accessToken: string
      refreshToken: string
      expiresAt: number
      refreshExpiresAt: number
      sessionId: string
    }
    policies?: Record<string, { allowed: boolean; reason: string | null }>
    challengeToken?: string
  }
}

export {}
