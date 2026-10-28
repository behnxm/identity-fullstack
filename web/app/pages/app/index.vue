<script setup lang="ts">
definePageMeta({
  layout: 'user'
})

const { user } = useUserSession()
const { policies, can } = useCapabilities()
const { refreshPolicies } = useBff()

const refreshing = ref(false)

const handleRefresh = async () => {
  refreshing.value = true
  try {
    await refreshPolicies()
  } finally {
    refreshing.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Welcome Header -->
    <div class="bg-white dark:bg-neutral-900 rounded-2xl p-6 border border-neutral-200 dark:border-neutral-800 shadow-sm flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-neutral-900 dark:text-white">
          Welcome, {{ user?.displayName }}
        </h1>
        <p class="text-xs text-neutral-500 mt-1">
          Identity: <span class="font-mono text-neutral-700 dark:text-neutral-300">{{ user?.email }}</span>
        </p>
      </div>

      <div class="flex items-center gap-2">
        <button
          type="button"
          :disabled="refreshing"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-neutral-100 dark:bg-neutral-800 hover:bg-neutral-200 dark:hover:bg-neutral-700 text-neutral-800 dark:text-neutral-200 transition-colors"
          @click="handleRefresh"
        >
          <span v-if="refreshing">Syncing...</span>
          <span v-else>↻ Refresh Policies</span>
        </button>

        <NuxtLink
          v-if="can('panel.admin')"
          to="/admin"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-amber-500 hover:bg-amber-600 text-white transition-colors"
        >
          Admin Console →
        </NuxtLink>
      </div>
    </div>

    <!-- Quick Stats Cards -->
    <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
      <div class="bg-white dark:bg-neutral-900 rounded-xl p-5 border border-neutral-200 dark:border-neutral-800">
        <div class="text-xs font-medium text-neutral-500 mb-1">Assigned Roles</div>
        <div class="flex flex-wrap gap-1.5 mt-2">
          <span
            v-for="role in (user?.roles || [])"
            :key="role"
            class="px-2 py-0.5 rounded text-xs font-semibold bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800"
          >
            {{ role }}
          </span>
          <span v-if="!user?.roles?.length" class="text-xs text-neutral-400">None</span>
        </div>
      </div>

      <div class="bg-white dark:bg-neutral-900 rounded-xl p-5 border border-neutral-200 dark:border-neutral-800">
        <div class="text-xs font-medium text-neutral-500 mb-1">Authentication Level (AMR)</div>
        <div class="flex flex-wrap gap-1.5 mt-2">
          <span
            v-for="amr in (user?.amr || [])"
            :key="amr"
            class="px-2 py-0.5 rounded text-xs font-mono font-semibold"
            :class="amr === 'mfa' ? 'bg-purple-100 dark:bg-purple-950 text-purple-700 dark:text-purple-300' : 'bg-neutral-100 dark:bg-neutral-800 text-neutral-700 dark:text-neutral-300'"
          >
            {{ amr }}
          </span>
        </div>
      </div>

      <div class="bg-white dark:bg-neutral-900 rounded-xl p-5 border border-neutral-200 dark:border-neutral-800">
        <div class="text-xs font-medium text-neutral-500 mb-1">MFA Status</div>
        <div class="mt-2 flex items-center space-x-2">
          <span
            class="inline-block w-2.5 h-2.5 rounded-full"
            :class="user?.twoFactorEnabled ? 'bg-emerald-500' : 'bg-amber-500'"
          />
          <span class="text-xs font-semibold text-neutral-800 dark:text-neutral-200">
            {{ user?.twoFactorEnabled ? 'Enrolled & Active' : 'Not Enrolled' }}
          </span>
        </div>
      </div>
    </div>

    <!-- Server Policy Evaluation Matrix -->
    <div class="bg-white dark:bg-neutral-900 rounded-2xl border border-neutral-200 dark:border-neutral-800 overflow-hidden shadow-sm">
      <div class="px-6 py-4 border-b border-neutral-200 dark:border-neutral-800 flex items-center justify-between">
        <div>
          <h2 class="text-base font-semibold text-neutral-900 dark:text-white">
            Server Policy Evaluation
          </h2>
          <p class="text-xs text-neutral-500 mt-0.5">
            Real-time server authorization policies evaluated by ASP.NET Core <code class="font-mono text-emerald-600 dark:text-emerald-400">IAuthorizationService</code>.
          </p>
        </div>
        <span class="text-xs font-mono text-neutral-400">GET /me/policies</span>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full text-left text-xs">
          <thead class="bg-neutral-50 dark:bg-neutral-800/50 text-neutral-500 font-semibold border-b border-neutral-200 dark:border-neutral-800">
            <tr>
              <th class="px-6 py-3">Policy</th>
              <th class="px-6 py-3">Status</th>
              <th class="px-6 py-3">Reason / Details</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-neutral-200 dark:divide-neutral-800">
            <tr v-for="(policy, key) in policies" :key="key" class="hover:bg-neutral-50 dark:hover:bg-neutral-800/30">
              <td class="px-6 py-3 font-mono font-semibold text-neutral-900 dark:text-neutral-100">
                {{ key }}
              </td>
              <td class="px-6 py-3">
                <span
                  class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold"
                  :class="policy.allowed ? 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300' : 'bg-red-100 dark:bg-red-950/60 text-red-800 dark:text-red-300'"
                >
                  {{ policy.allowed ? 'Allowed' : 'Denied' }}
                </span>
              </td>
              <td class="px-6 py-3 text-neutral-500">
                <span v-if="policy.reason" class="font-mono text-[11px] text-amber-600 dark:text-amber-400">
                  {{ policy.reason }}
                </span>
                <span v-else class="text-neutral-400">—</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
