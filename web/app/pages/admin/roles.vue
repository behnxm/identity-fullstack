<script setup lang="ts">
definePageMeta({
  layout: 'admin',
  policy: 'users.read'
})

interface RoleResponse {
  name: string
  description: string | null
  permissions: string[]
}

const { proxyGet } = useBff()

const roles = ref<RoleResponse[]>([])
const loading = ref(true)
const errorMessage = ref<string | null>(null)

const loadRoles = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    roles.value = await proxyGet<RoleResponse[]>('/admin/roles')
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to load system roles.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadRoles()
})
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold tracking-tight text-white">
        Roles & Permission Claims
      </h1>
      <p class="text-xs text-neutral-400 mt-0.5">
        Immutable role permission bundles seeded into ASP.NET Core Identity role-claims.
      </p>
    </div>

    <!-- Architectural Note Callout -->
    <div class="bg-amber-500/10 border border-amber-500/30 rounded-2xl p-5 text-xs text-neutral-300 space-y-1">
      <div class="font-bold text-amber-400">
        Principle: Roles are Bundles of Permissions, Never Authorization Checks
      </div>
      <p class="text-[11px] leading-relaxed text-neutral-400">
        In this system, <code class="font-mono text-neutral-200">[Authorize(Roles = "...")]</code> is strictly prohibited and guarded by automated architecture unit tests.
        Roles serve solely to bundle fine-grained permissions. When access tokens are minted, role claims of type <code class="font-mono text-neutral-200">permission</code> are embedded directly into the JWT payload for stateless authorization.
      </p>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      class="p-4 rounded-lg bg-red-950/40 border border-red-900 text-xs text-red-300 font-medium"
      role="alert"
    >
      {{ errorMessage }}
    </div>

    <div v-if="loading" class="py-12 text-center text-xs text-neutral-400">
      Loading roles and permissions matrix...
    </div>

    <!-- Roles Grid -->
    <div v-else class="grid grid-cols-1 md:grid-cols-3 gap-6">
      <div
        v-for="r in roles"
        :key="r.name"
        class="bg-neutral-900 border border-neutral-800 rounded-2xl p-6 flex flex-col justify-between shadow-sm"
      >
        <div>
          <div class="flex items-center justify-between mb-3">
            <h2 class="text-lg font-bold text-white">{{ r.name }}</h2>
            <span
              class="px-2 py-0.5 rounded text-[10px] font-semibold"
              :class="r.name === 'Admin' ? 'bg-amber-500/20 text-amber-300 border border-amber-500/40' : (r.name === 'Support' ? 'bg-blue-500/20 text-blue-300 border border-blue-500/40' : 'bg-neutral-800 text-neutral-300 border border-neutral-700')"
            >
              {{ r.permissions.length }} permissions
            </span>
          </div>

          <p class="text-xs text-neutral-400 mb-4 min-h-[36px]">
            {{ r.description || 'System role definition.' }}
          </p>

          <div class="space-y-1.5 pt-2 border-t border-neutral-800">
            <div class="text-[10px] font-semibold text-neutral-500 uppercase tracking-wider mb-2">
              Assigned Claims (type: permission)
            </div>
            <div
              v-for="p in r.permissions"
              :key="p"
              class="flex items-center space-x-2 text-xs font-mono text-neutral-300 bg-neutral-950 px-2.5 py-1.5 rounded border border-neutral-800/80"
            >
              <span class="text-amber-400">❖</span>
              <span>{{ p }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
