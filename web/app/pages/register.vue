<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  auth: false
})

const router = useRouter()
const toast = useToast()
const { register } = useBff()

const displayName = ref('')
const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const loading = ref(false)
const errorMessage = ref<string | null>(null)

const handleRegister = async () => {
  if (password.value !== confirmPassword.value) {
    errorMessage.value = 'Passwords do not match.'
    return
  }
  if (password.value.length < 12) {
    errorMessage.value = 'Password must be at least 12 characters long.'
    return
  }

  loading.value = true
  errorMessage.value = null

  try {
    await register({
      displayName: displayName.value.trim(),
      email: email.value.trim(),
      password: password.value
    })

    toast.add({
      title: 'Registration completed',
      description: 'Your account has been created. Please check your email to activate your account.',
      color: 'success'
    })

    await router.push({
      path: '/login',
      query: { registered: '1' }
    })
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Registration failed.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div>
    <div class="mb-6 text-center">
      <h2 class="text-2xl font-bold text-neutral-900 dark:text-white tracking-tight">
        Create an account
      </h2>
      <p class="text-sm text-neutral-500 mt-1">
        Already have an account?
        <NuxtLink to="/login" class="text-emerald-600 hover:text-emerald-500 font-medium">
          Sign in
        </NuxtLink>
      </p>
    </div>

    <div
      v-if="errorMessage"
      class="mb-5 p-3 rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 font-medium"
      role="alert"
    >
      {{ errorMessage }}
    </div>

    <form class="space-y-4" @submit.prevent="handleRegister">
      <div>
        <label for="displayName" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
          Full Name / Display Name
        </label>
        <input
          id="displayName"
          v-model="displayName"
          type="text"
          required
          autocomplete="name"
          class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          placeholder="Jane Doe"
        >
      </div>

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
          placeholder="jane@example.com"
        >
      </div>

      <div>
        <label for="password" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
          Password <span class="text-neutral-400 font-normal">(min 12 characters)</span>
        </label>
        <input
          id="password"
          v-model="password"
          type="password"
          required
          minlength="12"
          autocomplete="new-password"
          class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
        >
      </div>

      <div>
        <label for="confirmPassword" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
          Confirm Password
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
        <span v-if="loading">Creating account...</span>
        <span v-else>Register</span>
      </button>
    </form>
  </div>
</template>
