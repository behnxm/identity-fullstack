export default defineNuxtRouteMiddleware((to) => {
  const { loggedIn, user, session } = useUserSession()
  const { can, reason, resolveLandingPath } = useCapabilities()

  const isPublic = to.meta.auth === false
  const isAuthenticated = loggedIn.value && !!user.value
  const policies = (session.value as any)?.policies || {}

  // 1. Unauthenticated users accessing protected pages
  if (!isAuthenticated && !isPublic) {
    const redirect = to.fullPath !== '/' ? `?redirect=${encodeURIComponent(to.fullPath)}` : ''
    return navigateTo(`/login${redirect}`)
  }

  // 2. Authenticated users accessing login/register pages
  if (isAuthenticated && isPublic && (to.path === '/login' || to.path === '/register' || to.path === '/login/two-factor')) {
    const landing = resolveLandingPath(policies)
    return navigateTo(landing)
  }

  // 3. Admin section protection
  if (to.path.startsWith('/admin')) {
    if (!can('panel.admin')) {
      if (reason('panel.admin') === 'mfa_required') {
        return navigateTo('/app/security?required_mfa=1')
      }
      return navigateTo('/403')
    }
  }

  // 4. User app section protection
  if (to.path.startsWith('/app')) {
    if (!can('panel.user')) {
      return navigateTo('/403')
    }
  }

  // 5. Explicit policy declaration on page meta
  const requiredPolicy = to.meta.policy as string | undefined
  if (requiredPolicy && !can(requiredPolicy)) {
    if (reason(requiredPolicy) === 'mfa_required') {
      return navigateTo('/app/security?required_mfa=1')
    }
    return navigateTo('/403')
  }
})
