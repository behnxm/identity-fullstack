<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  auth: false
})

const route = useRoute()
const router = useRouter()
const { resetPassword } = useBff()

const userId = ref('')
const token = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const loading = ref(false)
const errorMessage = ref<string | null>(null)
const success = ref(false)

onMounted(() => {
  userId.value = (route.query.userId as string) || ''

  if (typeof window !== 'undefined' && window.location.hash) {
    const rawHash = window.location.hash.startsWith('#')
      ? window.location.hash.substring(1)
      : window.location.hash

    const tokenMatch = rawHash.match(/(?:^|&)token=([^&]*)/)
    if (tokenMatch && tokenMatch[1]) {
      token.value = decodeURIComponent(tokenMatch[1].replace(/\+/g, '%2B'))
    }

    const userMatch = rawHash.match(/(?:^|&)userId=([^&]*)/)
    if (userMatch && userMatch[1] && !userId.value) {
      userId.value = decodeURIComponent(userMatch[1])
    }
  }

  if (!token.value && route.query.token) {
    token.value = String(route.query.token)
  }

  // Normalize: ensure any space converted from '+' in Base64 is restored
  if (token.value && token.value.includes(' ')) {
    token.value = token.value.replace(/ /g, '+')
  }

  if (!userId.value || !token.value) {
    errorMessage.value = 'Invalid or missing reset token. Please request a new password reset link.'
  }
})

const handleSubmit = async () => {
  if (newPassword.value !== confirmPassword.value) {
    errorMessage.value = 'Passwords do not match.'
    return
  }
  if (newPassword.value.length < 12) {
    errorMessage.value = 'Password must be at least 12 characters long.'
    return
  }

  loading.value = true
  errorMessage.value = null

  try {
    await resetPassword({
      userId: userId.value,
      token: token.value,
      newPassword: newPassword.value
    })
    success.value = true
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Password reset failed.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div>
    <div class="mb-6 text-center">
      <h2 class="text-2xl font-bold text-neutral-900 dark:text-white tracking-tight">
        Choose a new password
      </h2>
      <p class="text-sm text-neutral-500 mt-1">
        Choose a strong, unique password of at least 12 characters.
      </p>
    </div>

    <div
      v-if="errorMessage"
      class="mb-5 p-3 rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 font-medium"
      role="alert"
    >
      {{ errorMessage }}
    </div>

    <div v-if="success" class="space-y-4">
      <div class="p-4 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 text-xs text-emerald-800 dark:text-emerald-300 font-medium">
        Your password has been reset successfully. You can now log in with your new password.
      </div>
      <NuxtLink
        to="/login"
        class="block w-full text-center py-2.5 px-4 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors"
      >
        Sign In
      </NuxtLink>
    </div>

    <form v-else-if="token && userId" class="space-y-4" @submit.prevent="handleSubmit">
      <div>
        <label for="newPassword" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
          New Password <span class="text-neutral-400 font-normal">(min 12 characters)</span>
        </label>
        <input
          id="newPassword"
          v-model="newPassword"
          type="password"
          required
          minlength="12"
          autocomplete="new-password"
          class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
        >
      </div>

      <div>
        <label for="confirmPassword" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
          Confirm New Password
        </label>
        <input
          id="confirmPassword"
          v-model="confirmPassword"
          type="password"
          required
          autocomplete="new-password"
          class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
        >
      </div>

      <button
        type="submit"
        :disabled="loading"
        class="w-full py-2.5 px-4 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed shadow-sm"
      >
        <span v-if="loading">Resetting password...</span>
        <span v-else>Update Password</span>
      </button>
    </form>
  </div>
</template>
