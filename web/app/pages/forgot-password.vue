<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  auth: false
})

const { forgotPassword } = useBff()

const email = ref('')
const loading = ref(false)
const submitted = ref(false)

const handleSubmit = async () => {
  loading.value = true
  try {
    await forgotPassword(email.value.trim())
  } catch {
    // Intentionally ignore failure to preserve anti-enumeration
  } finally {
    loading.value = false
    submitted.value = true
  }
}
</script>

<template>
  <div>
    <div class="mb-6 text-center">
      <h2 class="text-2xl font-bold text-neutral-900 dark:text-white tracking-tight">
        Reset your password
      </h2>
      <p class="text-sm text-neutral-500 mt-1">
        Enter your email address and we will dispatch a reset link.
      </p>
    </div>

    <div v-if="submitted" class="space-y-4">
      <div class="p-4 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 text-xs text-emerald-800 dark:text-emerald-300">
        If an account exists for <span class="font-semibold">{{ email }}</span>, you will receive password reset instructions.
        Check your inbox (or Mailpit at <a href="http://localhost:8025" target="_blank" class="underline">localhost:8025</a> locally).
      </div>

      <NuxtLink
        to="/login"
        class="block w-full text-center py-2.5 px-4 text-sm font-semibold rounded-lg bg-neutral-200 dark:bg-neutral-800 hover:bg-neutral-300 dark:hover:bg-neutral-700 text-neutral-800 dark:text-neutral-200 transition-colors"
      >
        Return to Sign In
      </NuxtLink>
    </div>

    <form v-else class="space-y-4" @submit.prevent="handleSubmit">
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
          placeholder="name@example.com"
        >
      </div>

      <button
        type="submit"
        :disabled="loading || !email"
        class="w-full py-2.5 px-4 text-sm font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed shadow-sm"
      >
        <span v-if="loading">Sending reset link...</span>
        <span v-else>Send reset link</span>
      </button>

      <div class="text-center pt-2">
        <NuxtLink to="/login" class="text-xs text-neutral-500 hover:text-emerald-600 font-medium">
          ← Back to Sign In
        </NuxtLink>
      </div>
    </form>
  </div>
</template>
