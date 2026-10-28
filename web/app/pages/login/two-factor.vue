<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  auth: false
})

const route = useRoute()
const router = useRouter()
const { login2Fa } = useBff()
const { resolveLandingPath, safeRedirect } = useCapabilities()

const code = ref('')
const isRecovery = ref(false)
const loading = ref(false)
const errorMessage = ref<string | null>(null)
const mfaToken = ref<string | null>(null)

onMounted(() => {
  if (typeof window !== 'undefined') {
    const token = sessionStorage.getItem('pending_mfa_token')
    if (!token) {
      router.replace('/login')
      return
    }
    mfaToken.value = token
  }
})

const handleSubmit = async () => {
  if (!mfaToken.value) {
    router.replace('/login')
    return
  }

  loading.value = true
  errorMessage.value = null

  try {
    const res = await login2Fa({
      mfaToken: mfaToken.value,
      code: code.value.trim(),
      isRecoveryCode: isRecovery.value
    })

    if (typeof window !== 'undefined') {
      sessionStorage.removeItem('pending_mfa_token')
    }

    const fallback = resolveLandingPath(res.policies)
    const target = safeRedirect(route.query.redirect as string, fallback)
    await router.push(target)
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Invalid verification code.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div>
    <div class="mb-6 text-center">
      <div class="w-12 h-12 mx-auto rounded-full bg-emerald-100 dark:bg-emerald-950/60 border border-emerald-300 dark:border-emerald-800 flex items-center justify-center text-emerald-600 dark:text-emerald-300 mb-3 text-lg font-bold">
        2FA
      </div>
      <h2 class="text-2xl font-bold text-neutral-900 dark:text-white tracking-tight">
        Two-Factor Verification
      </h2>
      <p class="text-xs text-neutral-500 mt-1">
        {{ isRecovery ? 'Enter one of your emergency recovery codes' : 'Enter the 6-digit code from your authenticator app' }}
      </p>
    </div>

    <div
      v-if="errorMessage"
      class="mb-5 p-3 rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 font-medium"
      role="alert"
    >
      {{ errorMessage }}
    </div>

    <form class="space-y-4" @submit.prevent="handleSubmit">
      <div>
        <label for="code" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
          {{ isRecovery ? 'Recovery code' : '6-digit authenticator code' }}
        </label>
        <input
          id="code"
          v-model="code"
          type="text"
          required
          autofocus
          autocomplete="one-time-code"
          :maxlength="isRecovery ? 32 : 8"
          class="w-full px-3 py-2 text-center text-lg tracking-widest font-mono rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          :placeholder="isRecovery ? 'ABCD1234' : '123456'"
        >
      </div>

      <button
        type="submit"
        :disabled="loading || !code"
        class="w-full py-2.5 px-4 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed shadow-sm"
      >
        <span v-if="loading">Verifying...</span>
        <span v-else>Verify and Continue</span>
      </button>

      <div class="text-center pt-2">
        <button
          type="button"
          class="text-xs text-emerald-600 dark:text-emerald-400 hover:underline font-medium"
          @click="isRecovery = !isRecovery; code = ''"
        >
          {{ isRecovery ? 'Use authenticator code instead' : 'Lost your authenticator? Use an emergency recovery code' }}
        </button>
      </div>
    </form>
  </div>
</template>
