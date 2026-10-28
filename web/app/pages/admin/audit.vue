<script setup lang="ts">
definePageMeta({
  layout: 'admin',
  policy: 'audit.read'
})

interface SecurityEventResponse {
  id: string
  eventType: string
  userId: string | null
  userEmail: string | null
  ipAddress: string | null
  userAgent: string | null
  timestamp: string
  detailsJson: string | null
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

const { proxyGet } = useBff()

const events = ref<SecurityEventResponse[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = ref(15)
const selectedType = ref('')
const loading = ref(true)
const errorMessage = ref<string | null>(null)

// Details modal state
const activeDetails = ref<string | null>(null)

const eventTypes = [
  { label: 'All Security Events', value: '' },
  { label: 'Login Success', value: 'auth.login_success' },
  { label: 'Login Failed', value: 'auth.login_failed' },
  { label: 'Token Refreshed', value: 'auth.token_refreshed' },
  { label: 'Token Reuse Detected', value: 'auth.token_reuse_detected' },
  { label: 'Logout', value: 'auth.logout' },
  { label: 'Two-Factor Enabled', value: 'auth.2fa_enabled' },
  { label: 'Two-Factor Disabled', value: 'auth.2fa_disabled' },
  { label: 'Session Revoked', value: 'auth.session_revoked' },
  { label: 'User Locked', value: 'auth.user_locked' },
  { label: 'Password Changed', value: 'auth.password_changed' }
]

const loadEvents = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    const res = await proxyGet<PagedResult<SecurityEventResponse>>('/admin/audit-events', {
      eventType: selectedType.value || undefined,
      page: page.value,
      pageSize: pageSize.value
    })
    events.value = res.items
    totalCount.value = res.totalCount
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to query audit events.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadEvents()
})

const handleTypeChange = () => {
  page.value = 1
  loadEvents()
}

const formatJson = (raw: string | null) => {
  if (!raw) return 'No metadata recorded.'
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
}

const getBadgeClass = (eventType: string) => {
  if (eventType.includes('failed') || eventType.includes('reuse') || eventType.includes('locked')) {
    return 'bg-red-500/20 text-red-300 border-red-500/40'
  }
  if (eventType.includes('2fa') || eventType.includes('revoked') || eventType.includes('password')) {
    return 'bg-amber-500/20 text-amber-300 border-amber-500/40'
  }
  return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/40'
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-white">
          Security Audit Log
        </h1>
        <p class="text-xs text-neutral-400 mt-0.5">
          Tamper-evident audit trail of authentication lifecycle, session rotation, and policy events.
        </p>
      </div>

      <div class="w-full sm:w-64">
        <select
          v-model="selectedType"
          class="w-full px-3 py-2 text-xs rounded-lg border border-neutral-700 bg-neutral-800 text-white focus:outline-none focus:ring-2 focus:ring-amber-500"
          @change="handleTypeChange"
        >
          <option v-for="t in eventTypes" :key="t.value" :value="t.value">
            {{ t.label }}
          </option>
        </select>
      </div>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      class="p-4 rounded-lg bg-red-950/40 border border-red-900 text-xs text-red-300 font-medium"
      role="alert"
    >
      {{ errorMessage }}
    </div>

    <!-- Events Table -->
    <div class="bg-neutral-900 border border-neutral-800 rounded-2xl overflow-hidden shadow-sm">
      <div class="overflow-x-auto">
        <table class="w-full text-left text-xs">
          <thead class="bg-neutral-800/60 text-neutral-400 font-semibold border-b border-neutral-800">
            <tr>
              <th class="px-6 py-3">Timestamp</th>
              <th class="px-6 py-3">Event Type</th>
              <th class="px-6 py-3">Actor / Subject</th>
              <th class="px-6 py-3">Client IP & Agent</th>
              <th class="px-6 py-3 text-right">Payload</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-neutral-800">
            <tr v-if="loading">
              <td colspan="5" class="px-6 py-8 text-center text-neutral-400">
                Loading security audit stream...
              </td>
            </tr>
            <tr v-else-if="!events.length">
              <td colspan="5" class="px-6 py-8 text-center text-neutral-400">
                No security events found for the selected filter.
              </td>
            </tr>
            <tr v-for="e in events" :key="e.id" class="hover:bg-neutral-800/30">
              <td class="px-6 py-3 text-neutral-400 font-mono text-[11px] whitespace-nowrap">
                {{ new Date(e.timestamp).toLocaleString() }}
              </td>

              <td class="px-6 py-3">
                <span
                  class="px-2 py-0.5 rounded text-[10px] font-mono font-semibold border"
                  :class="getBadgeClass(e.eventType)"
                >
                  {{ e.eventType }}
                </span>
              </td>

              <td class="px-6 py-3 font-mono text-[11px] text-neutral-300">
                {{ e.userEmail || (e.userId ? e.userId.slice(0, 8) + '...' : 'System / Unauth') }}
              </td>

              <td class="px-6 py-3 text-[11px] text-neutral-400 max-w-xs truncate">
                <span class="font-mono text-neutral-300">{{ e.ipAddress || '—' }}</span>
                <span class="block truncate text-[10px] text-neutral-500">{{ e.userAgent || 'Unknown' }}</span>
              </td>

              <td class="px-6 py-3 text-right">
                <button
                  type="button"
                  class="text-xs text-amber-400 hover:underline font-semibold"
                  @click="activeDetails = formatJson(e.detailsJson)"
                >
                  Inspect
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div class="px-6 py-3 bg-neutral-800/40 border-t border-neutral-800 flex items-center justify-between text-xs text-neutral-400">
        <div>Total: {{ totalCount }} events</div>
        <div class="flex items-center gap-2">
          <button
            type="button"
            :disabled="page <= 1"
            class="px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 disabled:opacity-40 text-white"
            @click="page--; loadEvents()"
          >
            ← Prev
          </button>
          <span>Page {{ page }}</span>
          <button
            type="button"
            :disabled="page * pageSize >= totalCount"
            class="px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 disabled:opacity-40 text-white"
            @click="page++; loadEvents()"
          >
            Next →
          </button>
        </div>
      </div>
    </div>

    <!-- JSON Inspector Modal -->
    <div
      v-if="activeDetails !== null"
      class="fixed inset-0 z-50 bg-neutral-950/70 backdrop-blur-sm flex items-center justify-center p-4"
    >
      <div class="bg-neutral-900 border border-neutral-800 rounded-2xl max-w-xl w-full p-6 shadow-2xl space-y-4">
        <div class="flex items-center justify-between">
          <h2 class="text-sm font-bold text-white">Event Metadata Payload</h2>
          <button
            type="button"
            class="text-neutral-400 hover:text-white"
            @click="activeDetails = null"
          >
            ✕
          </button>
        </div>

        <pre class="bg-neutral-950 p-4 rounded-xl text-xs font-mono text-emerald-400 border border-neutral-800 overflow-x-auto max-h-96">{{ activeDetails }}</pre>

        <div class="flex justify-end">
          <button
            type="button"
            class="px-4 py-2 text-xs font-semibold rounded-lg bg-neutral-800 hover:bg-neutral-700 text-white"
            @click="activeDetails = null"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
