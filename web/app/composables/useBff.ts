export function useBff() {
  const router = useRouter()
  const { clear, fetch: fetchSession } = useUserSession()

  const clientFetch = async <T>(url: string, options: Parameters<typeof $fetch>[1] = {}): Promise<T> => {
    const headers = new Headers(options.headers || {})
    headers.set('X-Requested-With', 'XMLHttpRequest')

    try {
      return (await ($fetch as any)(url, {
        ...options,
        headers
      })) as T
    } catch (err: any) {
      if (err.statusCode === 401 && !url.includes('/api/auth/login')) {
        await clear()
        router.push('/login')
      }
      throw err
    }
  }

  // Generic proxy methods for internal API resources
  const proxyGet = <T>(path: string, query?: Record<string, any>) =>
    clientFetch<T>(`/api/proxy/${path.replace(/^\/+/, '')}`, { method: 'GET', query })

  const proxyPost = <T>(path: string, body?: any) =>
    clientFetch<T>(`/api/proxy/${path.replace(/^\/+/, '')}`, { method: 'POST', body })

  const proxyPut = <T>(path: string, body?: any) =>
    clientFetch<T>(`/api/proxy/${path.replace(/^\/+/, '')}`, { method: 'PUT', body })

  const proxyDelete = <T>(path: string, body?: any) =>
    clientFetch<T>(`/api/proxy/${path.replace(/^\/+/, '')}`, { method: 'DELETE', body })

  // Auth methods
  const login = async (
    payloadOrEmail: { email: string; password: string; rememberMe?: boolean } | string,
    password?: string,
    rememberMe = false
  ) => {
    const body = typeof payloadOrEmail === 'string'
      ? { email: payloadOrEmail, password: password!, rememberMe }
      : payloadOrEmail

    const result = await clientFetch<any>('/api/auth/login', {
      method: 'POST',
      body
    })
    await fetchSession()
    return result
  }

  const login2Fa = async (
    payloadOrToken: {
      challengeToken?: string
      mfaToken?: string
      code?: string
      recoveryCode?: string
      isRecoveryCode?: boolean
    } | string,
    code?: string,
    recoveryCode?: string
  ) => {
    let body: { challengeToken: string; code?: string; recoveryCode?: string }

    if (typeof payloadOrToken === 'string') {
      body = {
        challengeToken: payloadOrToken,
        code,
        recoveryCode
      }
    } else {
      const challengeToken = payloadOrToken.challengeToken || payloadOrToken.mfaToken || ''
      if (payloadOrToken.isRecoveryCode) {
        body = {
          challengeToken,
          recoveryCode: payloadOrToken.code || payloadOrToken.recoveryCode
        }
      } else {
        body = {
          challengeToken,
          code: payloadOrToken.code,
          recoveryCode: payloadOrToken.recoveryCode
        }
      }
    }

    const result = await clientFetch<any>('/api/auth/login-2fa', {
      method: 'POST',
      body
    })
    await fetchSession()
    return result
  }

  const logout = async () => {
    try {
      await clientFetch('/api/auth/logout', { method: 'POST' })
    } catch {
      // Continue cleanup even if server call failed
    } finally {
      try {
        await clear()
      } catch {
        // Fallback: session state cleared
      }
      router.push('/login')
    }
  }

  const logoutAll = async () => {
    try {
      await clientFetch('/api/auth/logout-all', { method: 'POST' })
    } catch {
      // Continue cleanup even if server call failed
    } finally {
      try {
        await clear()
      } catch {
        // Fallback: session state cleared
      }
      router.push('/login')
    }
  }

  const register = async (payload: { displayName: string; email: string; password: string }) => {
    return await clientFetch<{ status: string }>('/api/auth/register', {
      method: 'POST',
      body: payload
    })
  }

  const confirmEmail = async (payload: { userId: string; token: string }) => {
    return await clientFetch<{ status: string }>('/api/auth/confirm-email', {
      method: 'POST',
      body: payload
    })
  }

  const resendConfirmation = async (email: string) => {
    return await clientFetch<{ status: string }>('/api/auth/resend-confirmation', {
      method: 'POST',
      body: { email }
    })
  }

  const forgotPassword = async (email: string) => {
    return await clientFetch<{ status: string }>('/api/auth/forgot-password', {
      method: 'POST',
      body: { email }
    })
  }

  const resetPassword = async (payload: { userId: string; token: string; newPassword: string }) => {
    return await clientFetch<{ status: string }>('/api/auth/reset-password', {
      method: 'POST',
      body: payload
    })
  }

  const refreshPolicies = async () => {
    const result = await clientFetch<{ policies: Record<string, any> }>('/api/auth/refresh-policies', {
      method: 'POST'
    })
    await fetchSession()
    return result.policies
  }

  const enable2Fa = async (code: string) => {
    const result = await clientFetch<{ recoveryCodes: string[] }>('/api/auth/enable-2fa', {
      method: 'POST',
      body: { code }
    })
    await fetchSession()
    return result
  }

  const disable2Fa = async (payload: { code: string; password: string }) => {
    const result = await clientFetch<{ success: boolean }>('/api/auth/disable-2fa', {
      method: 'POST',
      body: payload
    })
    await fetchSession()
    return result
  }

  return {
    clientFetch,
    proxyGet,
    proxyPost,
    proxyPut,
    proxyDelete,
    login,
    login2Fa,
    login2fa: login2Fa,
    logout,
    logoutAll,
    register,
    confirmEmail,
    resendConfirmation,
    forgotPassword,
    resetPassword,
    refreshPolicies,
    enable2Fa,
    disable2Fa
  }
}
