<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  auth: false
})

const route = useRoute()
const router = useRouter()
const { login } = useBff()
const { resolveLandingPath, safeRedirect } = useCapabilities()

const email = ref('')
const password = ref('')
const rememberMe = ref(false)
const loading = ref(false)
const errorMessage = ref<string | null>(null)

const handleLogin = async () => {
  loading.value = true
  errorMessage.value = null

  try {
    const res = await login({
      email: email.value.trim(),
      password: password.value,
      rememberMe: rememberMe.value
    })

    if (res.status === 'mfa_required') {
      if (typeof window !== 'undefined' && res.mfaToken) {
        sessionStorage.setItem('pending_mfa_token', res.mfaToken)
      }
      await router.push({
        path: '/login/two-factor',
        query: route.query.redirect ? { redirect: route.query.redirect } : {}
      })
      return
    }

    // Standard login complete
    const fallback = resolveLandingPath(res.policies)
    const target = safeRedirect(route.query.redirect as string, fallback)
    await router.push(target)
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Invalid email or password.'
  } finally {
    loading.value = false
  }
}

const prefill = (demoEmail: string) => {
  email.value = demoEmail
  password.value = 'Password!12345678'
}
</script>

<template>
  <div>
    <div class="mb-6 text-center">
      <h2 class="text-2xl font-bold text-neutral-900 dark:text-white tracking-tight">
        Sign in to your account
      </h2>
      <p class="text-sm text-neutral-500 mt-1">
        Or
        <NuxtLink to="/register" class="text-emerald-600 hover:text-emerald-500 font-medium">
          create a new account
        </NuxtLink>
      </p>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      class="mb-5 p-3 rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 font-medium"
      role="alert"
    >
      {{ errorMessage }}
    </div>

    <form class="space-y-4" @submit.prevent="handleLogin">
      <div>
        <label for="email" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
          Email address
        </label>
        <input
          id="email"
          v-model="email"
          type="email"
          required
          autocomplete="email"
          class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          placeholder="admin@identity.local"
        >
      </div>

      <div>
        <div class="flex items-center justify-between mb-1">
          <label for="password" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300">
            Password
          </label>
          <NuxtLink to="/forgot-password" class="text-xs text-neutral-500 hover:text-emerald-600">
            Forgot password?
          </NuxtLink>
        </div>
        <input
          id="password"
          v-model="password"
          type="password"
          required
          autocomplete="current-password"
          class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
        >
      </div>

      <div class="flex items-center">
        <input
          id="rememberMe"
          v-model="rememberMe"
          type="checkbox"
          class="h-4 w-4 rounded border-neutral-300 text-emerald-600 focus:ring-emerald-500"
        >
        <label for="rememberMe" class="ml-2 block text-xs text-neutral-600 dark:text-neutral-400">
          Remember me for 30 days
        </label>
      </div>

      <button
        type="submit"
        :disabled="loading"
        class="w-full py-2.5 px-4 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed shadow-sm"
      >
        <span v-if="loading">Signing in...</span>
        <span v-else>Sign in</span>
      </button>
    </form>

    <!-- Quick Demo Prefill -->
    <div class="mt-8 pt-6 border-t border-neutral-200 dark:border-neutral-800">
      <div class="text-[11px] font-semibold text-neutral-400 uppercase tracking-wider mb-2.5 text-center">
        Demo Credentials (Password: Password!12345678)
      </div>
      <div class="grid grid-cols-3 gap-2">
        <button
          type="button"
          class="px-2 py-1.5 text-xs rounded border border-neutral-200 dark:border-neutral-700 hover:border-emerald-500 bg-neutral-50 dark:bg-neutral-800/60 font-medium text-neutral-700 dark:text-neutral-300 transition-colors"
          @click="prefill('admin@identity.local')"
        >
          Admin
        </button>
        <button
          type="button"
          class="px-2 py-1.5 text-xs rounded border border-neutral-200 dark:border-neutral-700 hover:border-emerald-500 bg-neutral-50 dark:bg-neutral-800/60 font-medium text-neutral-700 dark:text-neutral-300 transition-colors"
          @click="prefill('support@identity.local')"
        >
          Support
        </button>
        <button
          type="button"
          class="px-2 py-1.5 text-xs rounded border border-neutral-200 dark:border-neutral-700 hover:border-emerald-500 bg-neutral-50 dark:bg-neutral-800/60 font-medium text-neutral-700 dark:text-neutral-300 transition-colors"
          @click="prefill('user@identity.local')"
        >
          User
        </button>
      </div>
    </div>
  </div>
</template>
