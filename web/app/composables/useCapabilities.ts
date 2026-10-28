export interface PolicyResult {
  allowed: boolean
  reason: string | null
}

export function safeRedirect(targetUrl: string | null | undefined, fallback = '/app'): string {
  if (!targetUrl || typeof targetUrl !== 'string') {
    return fallback
  }

  // Strict check: must start with single '/', must not start with '//' or '/\', no colon (protocols)
  const trimmed = targetUrl.trim()
  if (!trimmed.startsWith('/') || trimmed.startsWith('//') || trimmed.startsWith('/\\') || trimmed.includes(':') || trimmed.includes('\\')) {
    return fallback
  }

  // Prevent control characters
  if (/[\u0000-\u001F\u007F-\u009F]/.test(trimmed)) {
    return fallback
  }

  return trimmed
}

export function resolveLandingPath(policies?: Record<string, PolicyResult> | null | undefined): string {
  if (!policies) {
    return '/login'
  }
  if (policies['panel.admin']?.allowed) {
    return '/admin'
  }
  if (policies['panel.admin']?.reason === 'mfa_required') {
    return '/app/security'
  }
  if (policies['panel.user']?.allowed) {
    return '/app'
  }
  return '/403'
}

export function useCapabilities() {
  const { session } = useUserSession()

  const policies = computed<Record<string, PolicyResult>>(() => {
    return (session.value as any)?.policies || {}
  })

  const can = (policyName: string): boolean => {
    return !!policies.value[policyName]?.allowed
  }

  const reason = (policyName: string): string | null => {
    return policies.value[policyName]?.reason ?? null
  }

  const resolveCallerLanding = (customPolicies?: Record<string, PolicyResult> | null): string => {
    return resolveLandingPath(customPolicies ?? policies.value)
  }

  return {
    policies,
    can,
    reason,
    resolveLandingPath: resolveCallerLanding,
    safeRedirect
  }
}
