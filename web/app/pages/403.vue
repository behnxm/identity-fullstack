<script setup lang="ts">
definePageMeta({
  auth: false
})

const route = useRoute()
const { loggedIn } = useUserSession()
const { resolveLandingPath, can } = useCapabilities()

const reason = computed(() => (route.query.reason as string) || 'Access Denied')
const requiredPolicy = computed(() => (route.query.policy as string) || '')

const homeDestination = computed(() => {
  if (!loggedIn.value) return '/login'
  return resolveLandingPath()
})
</script>

<template>
  <div class="min-h-screen bg-neutral-950 text-neutral-100 flex flex-col justify-center items-center px-4">
    <div class="max-w-md w-full bg-neutral-900 border border-neutral-800 rounded-2xl p-8 shadow-2xl text-center">
      <div class="w-16 h-16 mx-auto rounded-2xl bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400 text-2xl font-bold mb-6">
        403
      </div>

      <h1 class="text-2xl font-bold text-white mb-2">
        Access Forbidden
      </h1>

      <p class="text-sm text-neutral-400 mb-6 leading-relaxed">
        <span v-if="reason === 'mfa_required'">
          Access to this resource requires active Two-Factor Authentication. Please complete TOTP enrollment in your security settings to proceed.
        </span>
        <span v-else-if="requiredPolicy">
          Your account does not satisfy policy <code class="text-amber-400 font-mono text-xs">{{ requiredPolicy }}</code>. Server authorization policy prevents access.
        </span>
        <span v-else>
          You do not have the required permissions or policy entitlements to access this area.
        </span>
      </p>

      <div class="space-y-3">
        <NuxtLink
          v-if="reason === 'mfa_required'"
          to="/app/security?required_mfa=1"
          class="block w-full py-2.5 px-4 text-sm font-semibold rounded-lg bg-amber-500 hover:bg-amber-400 text-neutral-950 transition-colors font-semibold"
        >
          Enroll in Two-Factor Auth →
        </NuxtLink>

        <NuxtLink
          :to="homeDestination"
          class="block w-full py-2.5 px-4 text-sm font-semibold rounded-lg bg-neutral-800 hover:bg-neutral-700 text-white transition-colors"
        >
          Return to {{ loggedIn ? (can('panel.admin') ? 'Admin Console' : 'User Panel') : 'Home' }}
        </NuxtLink>
      </div>
    </div>
  </div>
</template>
