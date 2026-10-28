<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  auth: false
})

const route = useRoute()
const { resendConfirmation } = useBff()

const email = computed(() => (route.query.email as string) || '')
const resending = ref(false)
const resendNotice = ref<string | null>(null)

const handleResend = async () => {
  if (!email.value) return
  resending.value = true
  resendNotice.value = null
  try {
    await resendConfirmation(email.value)
    resendNotice.value = 'Confirmation link resent! Check your inbox.'
  } catch (err: any) {
    resendNotice.value = 'If that account exists, a link has been dispatched.'
  } finally {
    resending.value = false
  }
}
</script>

<template>
  <div class="text-center">
    <div class="w-12 h-12 mx-auto rounded-full bg-emerald-100 dark:bg-emerald-950/60 border border-emerald-300 dark:border-emerald-800 flex items-center justify-center text-emerald-600 dark:text-emerald-300 mb-3 text-lg font-bold">
      ✉
    </div>

    <h2 class="text-2xl font-bold text-neutral-900 dark:text-white tracking-tight mb-2">
      Check your email
    </h2>

    <p class="text-sm text-neutral-600 dark:text-neutral-400 mb-4">
      We sent a verification link to
      <span class="font-semibold text-neutral-900 dark:text-white">{{ email || 'your email' }}</span>.
      Click the link in the message to activate your account.
    </p>

    <!-- Mailpit Notice for local development -->
    <div class="p-3 mb-6 rounded-lg bg-neutral-100 dark:bg-neutral-800/80 border border-neutral-200 dark:border-neutral-700 text-xs text-neutral-600 dark:text-neutral-300">
      <span class="font-semibold">Local Development:</span> In local environments, emails are captured by Mailpit. Open
      <a href="http://localhost:8025" target="_blank" rel="noopener noreferrer" class="text-emerald-600 dark:text-emerald-400 font-semibold underline">
        http://localhost:8025
      </a>
      to view incoming verification messages.
    </div>

    <div v-if="resendNotice" class="mb-4 text-xs font-medium text-emerald-600 dark:text-emerald-400">
      {{ resendNotice }}
    </div>

    <div class="space-y-3">
      <button
        v-if="email"
        type="button"
        :disabled="resending"
        class="w-full py-2 px-4 text-xs font-semibold rounded-lg bg-neutral-200 dark:bg-neutral-800 hover:bg-neutral-300 dark:hover:bg-neutral-700 text-neutral-800 dark:text-neutral-200 transition-colors"
        @click="handleResend"
      >
        <span v-if="resending">Sending...</span>
        <span v-else>Resend confirmation link</span>
      </button>

      <NuxtLink
        to="/login"
        class="block w-full py-2.5 px-4 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors"
      >
        Back to Sign In
      </NuxtLink>
    </div>
  </div>
</template>
