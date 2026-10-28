<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  auth: false
})

const route = useRoute()
const { confirmEmail } = useBff()

const status = ref<'verifying' | 'success' | 'error'>('verifying')
const message = ref('Confirming your email address...')

onMounted(async () => {
  let userId = (route.query.userId as string) || ''
  let token = ''

  if (typeof window !== 'undefined' && window.location.hash) {
    const rawHash = window.location.hash.startsWith('#')
      ? window.location.hash.substring(1)
      : window.location.hash
    const params = new URLSearchParams(rawHash)
    token = params.get('token') || ''
    if (!userId) {
      userId = params.get('userId') || ''
    }
  }

  // Fallback to query param if hash is empty
  if (!token && route.query.token) {
    token = route.query.token as string
  }

  if (!userId || !token) {
    status.value = 'error'
    message.value = 'Invalid or missing confirmation link parameters. Please request a new confirmation link.'
    return
  }

  try {
    await confirmEmail({ userId, token })
    status.value = 'success'
    message.value = 'Your email address has been verified successfully. You may now sign in.'
  } catch (err: any) {
    status.value = 'error'
    message.value = err?.data?.detail || err?.data?.title || err?.message || 'Verification failed or the confirmation token has expired.'
  }
})
</script>

<template>
  <div class="text-center">
    <div
      class="w-12 h-12 mx-auto rounded-full flex items-center justify-center text-lg font-bold mb-4"
      :class="[
        status === 'verifying' ? 'bg-neutral-100 dark:bg-neutral-800 text-neutral-500 animate-spin' : '',
        status === 'success' ? 'bg-emerald-100 dark:bg-emerald-950/60 border border-emerald-300 dark:border-emerald-800 text-emerald-600 dark:text-emerald-300' : '',
        status === 'error' ? 'bg-red-100 dark:bg-red-950/60 border border-red-300 dark:border-red-800 text-red-600 dark:text-red-300' : ''
      ]"
    >
      <span v-if="status === 'verifying'">⏳</span>
      <span v-else-if="status === 'success'">✓</span>
      <span v-else>✕</span>
    </div>

    <h2 class="text-2xl font-bold text-neutral-900 dark:text-white tracking-tight mb-2">
      <span v-if="status === 'verifying'">Verifying Email</span>
      <span v-else-if="status === 'success'">Email Verified!</span>
      <span v-else>Verification Failed</span>
    </h2>

    <p class="text-sm text-neutral-600 dark:text-neutral-400 mb-6">
      {{ message }}
    </p>

    <div v-if="status === 'success'" class="space-y-3">
      <NuxtLink
        to="/login"
        class="block w-full py-2.5 px-4 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors"
      >
        Sign in to your account
      </NuxtLink>
    </div>

    <div v-if="status === 'error'" class="space-y-3">
      <NuxtLink
        to="/login"
        class="block w-full py-2.5 px-4 text-sm font-semibold rounded-lg bg-neutral-200 dark:bg-neutral-800 hover:bg-neutral-300 dark:hover:bg-neutral-700 text-neutral-800 dark:text-neutral-200 transition-colors"
      >
        Back to Sign In
      </NuxtLink>
    </div>
  </div>
</template>
