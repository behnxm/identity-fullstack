<script setup lang="ts">
definePageMeta({
  layout: 'user'
})

const { user, fetch: fetchSession } = useUserSession()
const { proxyPut } = useBff()

const displayName = ref(user.value?.displayName || '')
const saving = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

watchEffect(() => {
  if (user.value?.displayName && !saving.value) {
    displayName.value = user.value.displayName
  }
})

const handleUpdate = async () => {
  if (!displayName.value.trim()) return
  saving.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    await proxyPut('/me/profile', {
      displayName: displayName.value.trim()
    })
    await fetchSession()
    successMessage.value = 'Profile updated successfully.'
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to update profile.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold tracking-tight text-neutral-900 dark:text-white">
        Profile Settings
      </h1>
      <p class="text-xs text-neutral-500 mt-0.5">
        Manage your user account display details.
      </p>
    </div>

    <div
      v-if="errorMessage"
      class="p-4 rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 font-medium"
      role="alert"
    >
      {{ errorMessage }}
    </div>

    <div
      v-if="successMessage"
      class="p-4 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 text-xs text-emerald-700 dark:text-emerald-300 font-medium"
    >
      {{ successMessage }}
    </div>

    <div class="bg-white dark:bg-neutral-900 rounded-2xl p-6 border border-neutral-200 dark:border-neutral-800 shadow-sm space-y-6">
      <form class="space-y-4" @submit.prevent="handleUpdate">
        <div>
          <label for="displayName" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
            Display Name
          </label>
          <input
            id="displayName"
            v-model="displayName"
            type="text"
            required
            minlength="2"
            maxlength="100"
            class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
        </div>

        <div>
          <label class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
            Email Address
          </label>
          <input
            :value="user?.email"
            type="email"
            disabled
            class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-200 dark:border-neutral-800 bg-neutral-100 dark:bg-neutral-800/40 text-neutral-500 cursor-not-allowed"
          >
          <p class="text-[11px] text-neutral-400 mt-1">
            Email change is a deliberate non-goal in this reference architecture.
          </p>
        </div>

        <div class="pt-2">
          <button
            type="submit"
            :disabled="saving || !displayName.trim()"
            class="px-4 py-2 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors disabled:opacity-50"
          >
            <span v-if="saving">Updating...</span>
            <span v-else>Save Changes</span>
          </button>
        </div>
      </form>
    </div>
  </div>
</template>
