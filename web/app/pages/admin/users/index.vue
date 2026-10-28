<script setup lang="ts">
definePageMeta({
  layout: 'admin',
  policy: 'users.read'
})

interface AdminUserResponse {
  id: string
  email: string
  displayName: string
  emailConfirmed: boolean
  twoFactorEnabled: boolean
  isLockedOut: boolean
  lockoutEnd: string | null
  createdAt: string
  lastLoginAt: string | null
  roles: string[]
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

const { can } = useCapabilities()
const { proxyGet, proxyPost, proxyPut } = useBff()

const users = ref<AdminUserResponse[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = ref(10)
const search = ref('')
const selectedRole = ref('')
const loading = ref(true)
const errorMessage = ref<string | null>(null)
const actionLoading = ref(false)

// Role management modal state
const showRolesModal = ref(false)
const targetUser = ref<AdminUserResponse | null>(null)
const selectedRoles = ref<string[]>([])
const availableRoles = ['User', 'Support', 'Admin']

const loadUsers = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    const res = await proxyGet<PagedResult<AdminUserResponse>>('/admin/users', {
      search: search.value.trim() || undefined,
      role: selectedRole.value || undefined,
      page: page.value,
      pageSize: pageSize.value
    })
    users.value = res.items
    totalCount.value = res.totalCount
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to query users.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadUsers()
})

const handleSearch = () => {
  page.value = 1
  loadUsers()
}

const handleLockToggle = async (user: AdminUserResponse) => {
  const action = user.isLockedOut ? 'unlock' : 'lock'
  if (!confirm(`Are you sure you want to ${action} ${user.email}?`)) return

  actionLoading.value = true
  try {
    await proxyPost(`/admin/users/${user.id}/${action}`)
    await loadUsers()
  } catch (err: any) {
    alert(err?.data?.detail || `Failed to ${action} user.`)
  } finally {
    actionLoading.value = false
  }
}

const handleRevokeSessions = async (user: AdminUserResponse) => {
  if (!confirm(`Revoke all active sessions for ${user.email}? The user will be required to log in again.`)) return

  actionLoading.value = true
  try {
    await proxyPost(`/admin/users/${user.id}/revoke-sessions`)
    alert('User sessions revoked successfully.')
  } catch (err: any) {
    alert(err?.data?.detail || 'Failed to revoke sessions.')
  } finally {
    actionLoading.value = false
  }
}

const handleResendConfirmation = async (user: AdminUserResponse) => {
  actionLoading.value = true
  try {
    await proxyPost(`/admin/users/${user.id}/resend-confirmation`)
    alert(`Confirmation email dispatched for ${user.email}.`)
  } catch (err: any) {
    alert(err?.data?.detail || 'Failed to dispatch confirmation email.')
  } finally {
    actionLoading.value = false
  }
}

const openRolesModal = (user: AdminUserResponse) => {
  targetUser.value = user
  selectedRoles.value = [...user.roles]
  showRolesModal.value = true
}

const toggleRole = (role: string) => {
  if (selectedRoles.value.includes(role)) {
    selectedRoles.value = selectedRoles.value.filter(r => r !== role)
  } else {
    selectedRoles.value.push(role)
  }
}

const saveRoles = async () => {
  if (!targetUser.value) return
  actionLoading.value = true
  try {
    await proxyPut(`/admin/users/${targetUser.value.id}/roles`, {
      roles: selectedRoles.value
    })
    showRolesModal.value = false
    await loadUsers()
  } catch (err: any) {
    alert(err?.data?.detail || 'Failed to update roles.')
  } finally {
    actionLoading.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-white">
          Users Directory
        </h1>
        <p class="text-xs text-neutral-400 mt-0.5">
          User lifecycle management, role assignments, and session revocation.
        </p>
      </div>

      <!-- Support Persona Notice -->
      <span
        v-if="!can('users.manage')"
        class="text-xs px-3 py-1.5 rounded-lg bg-blue-950/60 text-blue-300 border border-blue-800"
      >
        Read-Only View (Requires <code class="font-mono">users.manage</code> for write actions)
      </span>
    </div>

    <!-- Filter Bar -->
    <div class="bg-neutral-900 border border-neutral-800 rounded-xl p-4 flex flex-col sm:flex-row gap-3">
      <div class="flex-1">
        <input
          v-model="search"
          type="text"
          placeholder="Search by email or name..."
          class="w-full px-3 py-2 text-xs rounded-lg border border-neutral-700 bg-neutral-800 text-white focus:outline-none focus:ring-2 focus:ring-amber-500"
          @keyup.enter="handleSearch"
        >
      </div>

      <div class="w-full sm:w-44">
        <select
          v-model="selectedRole"
          class="w-full px-3 py-2 text-xs rounded-lg border border-neutral-700 bg-neutral-800 text-white focus:outline-none focus:ring-2 focus:ring-amber-500"
          @change="handleSearch"
        >
          <option value="">All Roles</option>
          <option value="Admin">Admin</option>
          <option value="Support">Support</option>
          <option value="User">User</option>
        </select>
      </div>

      <button
        type="button"
        class="px-4 py-2 text-xs font-semibold rounded-lg bg-amber-500 hover:bg-amber-400 text-neutral-950 transition-colors"
        @click="handleSearch"
      >
        Filter
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

    <!-- Users Table -->
    <div class="bg-neutral-900 border border-neutral-800 rounded-2xl overflow-hidden shadow-sm">
      <div class="overflow-x-auto">
        <table class="w-full text-left text-xs">
          <thead class="bg-neutral-800/60 text-neutral-400 font-semibold border-b border-neutral-800">
            <tr>
              <th class="px-6 py-3">User / Email</th>
              <th class="px-6 py-3">Roles</th>
              <th class="px-6 py-3">Security Status</th>
              <th class="px-6 py-3">Last Login</th>
              <th class="px-6 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-neutral-800">
            <tr v-if="loading">
              <td colspan="5" class="px-6 py-8 text-center text-neutral-400">
                Loading users directory...
              </td>
            </tr>
            <tr v-else-if="!users.length">
              <td colspan="5" class="px-6 py-8 text-center text-neutral-400">
                No users match the search criteria.
              </td>
            </tr>
            <tr v-for="u in users" :key="u.id" class="hover:bg-neutral-800/30">
              <td class="px-6 py-4">
                <div class="font-semibold text-white">{{ u.displayName }}</div>
                <div class="text-[11px] text-neutral-400 font-mono">{{ u.email }}</div>
              </td>

              <td class="px-6 py-4">
                <div class="flex flex-wrap gap-1">
                  <span
                    v-for="r in u.roles"
                    :key="r"
                    class="px-2 py-0.5 rounded text-[10px] font-semibold"
                    :class="r === 'Admin' ? 'bg-amber-500/20 text-amber-300 border border-amber-500/40' : (r === 'Support' ? 'bg-blue-500/20 text-blue-300 border border-blue-500/40' : 'bg-neutral-800 text-neutral-300 border border-neutral-700')"
                  >
                    {{ r }}
                  </span>
                </div>
              </td>

              <td class="px-6 py-4 space-y-1">
                <div class="flex items-center gap-1.5">
                  <span
                    class="w-2 h-2 rounded-full"
                    :class="u.isLockedOut ? 'bg-red-500' : 'bg-emerald-500'"
                  />
                  <span class="text-[11px] text-neutral-300">
                    {{ u.isLockedOut ? 'Locked Out' : 'Active' }}
                  </span>
                </div>
                <div class="flex items-center gap-2 text-[10px] text-neutral-400">
                  <span>{{ u.emailConfirmed ? '✓ Confirmed' : '⚠ Unconfirmed' }}</span>
                  <span>{{ u.twoFactorEnabled ? '✓ 2FA Active' : '✕ No 2FA' }}</span>
                </div>
              </td>

              <td class="px-6 py-4 text-neutral-400 text-[11px]">
                {{ u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString() : 'Never' }}
              </td>

              <td class="px-6 py-4 text-right space-x-2">
                <template v-if="can('users.manage')">
                  <button
                    type="button"
                    :disabled="actionLoading"
                    class="text-xs text-amber-400 hover:underline font-semibold"
                    @click="openRolesModal(u)"
                  >
                    Roles
                  </button>

                  <button
                    type="button"
                    :disabled="actionLoading"
                    class="text-xs hover:underline font-semibold"
                    :class="u.isLockedOut ? 'text-emerald-400' : 'text-red-400'"
                    @click="handleLockToggle(u)"
                  >
                    {{ u.isLockedOut ? 'Unlock' : 'Lock' }}
                  </button>

                  <button
                    type="button"
                    :disabled="actionLoading"
                    class="text-xs text-neutral-400 hover:text-white underline"
                    @click="handleRevokeSessions(u)"
                  >
                    Revoke
                  </button>

                  <button
                    v-if="!u.emailConfirmed"
                    type="button"
                    :disabled="actionLoading"
                    class="text-xs text-blue-400 hover:underline"
                    @click="handleResendConfirmation(u)"
                  >
                    Resend
                  </button>
                </template>
                <template v-else>
                  <span class="text-[11px] text-neutral-500 italic">Read-only</span>
                </template>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div class="px-6 py-3 bg-neutral-800/40 border-t border-neutral-800 flex items-center justify-between text-xs text-neutral-400">
        <div>Total: {{ totalCount }} users</div>
        <div class="flex items-center gap-2">
          <button
            type="button"
            :disabled="page <= 1"
            class="px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 disabled:opacity-40 text-white"
            @click="page--; loadUsers()"
          >
            ← Prev
          </button>
          <span>Page {{ page }}</span>
          <button
            type="button"
            :disabled="page * pageSize >= totalCount"
            class="px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 disabled:opacity-40 text-white"
            @click="page++; loadUsers()"
          >
            Next →
          </button>
        </div>
      </div>
    </div>

    <!-- Edit Roles Modal -->
    <div
      v-if="showRolesModal && targetUser"
      class="fixed inset-0 z-50 bg-neutral-950/70 backdrop-blur-sm flex items-center justify-center p-4"
    >
      <div class="bg-neutral-900 border border-neutral-800 rounded-2xl max-w-md w-full p-6 shadow-2xl">
        <h2 class="text-lg font-bold text-white mb-1">
          Manage Roles for {{ targetUser.displayName }}
        </h2>
        <p class="text-xs text-neutral-400 mb-4">
          Assigning or removing roles automatically revokes all of this user's active sessions to force re-evaluation of permission claims.
        </p>

        <div class="space-y-2 mb-6">
          <label
            v-for="r in availableRoles"
            :key="r"
            class="flex items-center p-3 rounded-lg border border-neutral-800 hover:bg-neutral-800/50 cursor-pointer"
          >
            <input
              type="checkbox"
              :checked="selectedRoles.includes(r)"
              class="h-4 w-4 rounded border-neutral-700 text-amber-500 focus:ring-amber-500"
              @change="toggleRole(r)"
            >
            <span class="ml-3 text-sm font-semibold text-white">{{ r }}</span>
          </label>
        </div>

        <div class="flex items-center justify-end space-x-2">
          <button
            type="button"
            class="px-3 py-2 text-xs font-medium rounded-lg text-neutral-400 hover:bg-neutral-800"
            @click="showRolesModal = false"
          >
            Cancel
          </button>
          <button
            type="button"
            :disabled="actionLoading"
            class="px-4 py-2 text-xs font-semibold rounded-lg bg-amber-500 hover:bg-amber-400 text-neutral-950 transition-colors disabled:opacity-50"
            @click="saveRoles"
          >
            <span v-if="actionLoading">Saving...</span>
            <span v-else>Save Roles</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
