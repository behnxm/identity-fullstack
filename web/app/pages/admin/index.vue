<script setup lang="ts">
definePageMeta({
  layout: 'admin'
})

interface AdminStatsResponse {
  totalUsers: number
  confirmedUsers: number
  lockedUsers: number
  twoFactorPercentage: number
  activeSessionsCount: number
  failedLoginsLast24Hours: number
}

const { user } = useUserSession()
const { can } = useCapabilities()
const { proxyGet } = useBff()

const stats = ref<AdminStatsResponse | null>(null)
const loading = ref(true)
const errorMessage = ref<string | null>(null)

const loadStats = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    stats.value = await proxyGet<AdminStatsResponse>('/admin/stats')
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to load system statistics.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadStats()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="bg-neutral-900 border border-neutral-800 rounded-2xl p-6 shadow-sm flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <div class="flex items-center gap-2 mb-1">
          <h1 class="text-2xl font-bold tracking-tight text-white">
            Admin Console Overview
          </h1>
          <span
            class="text-xs px-2.5 py-0.5 rounded-full font-semibold"
            :class="can('users.manage') ? 'bg-amber-500/20 text-amber-300 border border-amber-500/40' : 'bg-blue-500/20 text-blue-300 border border-blue-500/40'"
          >
            {{ can('users.manage') ? 'Full Admin Persona' : 'Support Persona (Reduced)' }}
          </span>
        </div>
        <p class="text-xs text-neutral-400">
          Zero-trust administration dashboard. Enforces <code class="text-amber-400 font-mono">panel.admin</code> and active multi-factor authentication.
        </p>
      </div>

      <button
        type="button"
        :disabled="loading"
        class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-200 transition-colors border border-neutral-700"
        @click="loadStats"
      >
        <span v-if="loading">Refreshing...</span>
        <span v-else>↻ Refresh Stats</span>
      </button>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      class="p-4 rounded-lg bg-red-950/40 border border-red-900 text-xs text-red-300 font-medium"
      role="alert"
    >
      {{ errorMessage }}
    </div>

    <!-- Stats Grid -->
    <div v-if="loading" class="py-12 text-center text-xs text-neutral-400">
      Loading administration metrics...
    </div>

    <div v-else-if="stats" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <div class="bg-neutral-900/90 rounded-xl p-5 border border-neutral-800">
        <div class="text-xs font-medium text-neutral-400">Total Registered Users</div>
        <div class="text-2xl font-black text-white mt-2">{{ stats.totalUsers }}</div>
        <div class="text-[11px] text-neutral-500 mt-1">
          {{ stats.confirmedUsers }} confirmed accounts
        </div>
      </div>

      <div class="bg-neutral-900/90 rounded-xl p-5 border border-neutral-800">
        <div class="text-xs font-medium text-neutral-400">Two-Factor Adoption</div>
        <div class="text-2xl font-black text-emerald-400 mt-2">
          {{ stats.twoFactorPercentage.toFixed(1) }}%
        </div>
        <div class="text-[11px] text-neutral-500 mt-1">RFC 6238 TOTP enrolled</div>
      </div>

      <div class="bg-neutral-900/90 rounded-xl p-5 border border-neutral-800">
        <div class="text-xs font-medium text-neutral-400">Active Refresh Sessions</div>
        <div class="text-2xl font-black text-amber-400 mt-2">{{ stats.activeSessionsCount }}</div>
        <div class="text-[11px] text-neutral-500 mt-1">Rotating cryptographic sessions</div>
      </div>

      <div class="bg-neutral-900/90 rounded-xl p-5 border border-neutral-800">
        <div class="text-xs font-medium text-neutral-400">Locked Accounts</div>
        <div class="text-2xl font-black text-red-400 mt-2">{{ stats.lockedUsers }}</div>
        <div class="text-[11px] text-neutral-500 mt-1">Administrative & brute-force locks</div>
      </div>

      <div class="bg-neutral-900/90 rounded-xl p-5 border border-neutral-800">
        <div class="text-xs font-medium text-neutral-400">Failed Logins (Last 24h)</div>
        <div class="text-2xl font-black text-purple-400 mt-2">{{ stats.failedLoginsLast24Hours }}</div>
        <div class="text-[11px] text-neutral-500 mt-1">Monitored by audit pipeline</div>
      </div>

      <div class="bg-neutral-900/90 rounded-xl p-5 border border-neutral-800">
        <div class="text-xs font-medium text-neutral-400">Caller Identity</div>
        <div class="text-sm font-bold text-white mt-2 truncate">{{ user?.email }}</div>
        <div class="text-[11px] text-neutral-500 mt-1 truncate">
          {{ (user?.roles || []).join(', ') }}
        </div>
      </div>
    </div>

    <!-- Persona & Capability Banner -->
    <div class="bg-neutral-900/60 rounded-2xl p-6 border border-neutral-800 space-y-3">
      <h2 class="text-sm font-semibold text-white">
        Role-to-Policy Mapping for Current Session
      </h2>
      <p class="text-xs text-neutral-400 leading-relaxed">
        In this zero-trust architecture, user interface navigation and backend endpoints depend exclusively on fine-grained server policies rather than role names.
      </p>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2 text-xs">
        <div class="p-3 bg-neutral-950 rounded-lg border border-neutral-800 space-y-1">
          <span class="font-semibold text-emerald-400">Granted Administrative Policies</span>
          <ul class="space-y-0.5 text-neutral-300 font-mono text-[11px]">
            <li v-if="can('users.read')">✓ users.read (Query user directory)</li>
            <li v-if="can('audit.read')">✓ audit.read (Security audit events)</li>
            <li v-if="can('users.manage')">✓ users.manage (Account locks, roles, session revocation)</li>
            <li v-if="can('notes.moderate')">✓ notes.moderate (Global note content moderation)</li>
          </ul>
        </div>

        <div class="p-3 bg-neutral-950 rounded-lg border border-neutral-800 space-y-1">
          <span class="font-semibold text-amber-400">Enforcement Model</span>
          <p class="text-neutral-400 text-[11px]">
            Endpoints enforce strict authorization policies. Even if a user could craft client requests, the API's <code class="font-mono text-neutral-300">IAuthorizationMiddlewareResultHandler</code> returns RFC 9457 ProblemDetails with HTTP 403 Forbidden.
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
