<script setup lang="ts">
const { user } = useUserSession()
const { can } = useCapabilities()
const { logout } = useBff()

const navItems = computed(() => [
  { label: 'Overview', to: '/admin', show: true },
  { label: 'Users Directory', to: '/admin/users', show: can('users.read') },
  { label: 'Roles & Claims', to: '/admin/roles', show: true },
  { label: 'Security Audit Log', to: '/admin/audit', show: can('audit.read') },
  { label: 'Notes Moderation', to: '/admin/notes', show: can('notes.moderate') }
].filter(item => item.show))
</script>

<template>
  <div class="min-h-screen bg-neutral-950 text-neutral-100 flex flex-col">
    <!-- Top Bar -->
    <header class="bg-neutral-900 border-b border-amber-900/40 sticky top-0 z-30">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        <div class="flex items-center space-x-3">
          <div class="w-8 h-8 rounded-lg bg-amber-500 text-neutral-950 flex items-center justify-center font-bold text-sm shadow-md shadow-amber-500/20">
            AC
          </div>
          <span class="font-bold text-lg tracking-tight text-white">Admin Console</span>
          <span class="text-xs px-2.5 py-0.5 rounded-full bg-amber-950/80 text-amber-300 font-medium border border-amber-700/60">
            panel.admin
          </span>
          <span
            v-if="user?.amr?.includes('mfa')"
            class="text-[10px] px-2 py-0.5 rounded bg-emerald-950/80 text-emerald-300 font-medium border border-emerald-700/50"
          >
            MFA Active
          </span>
        </div>

        <div class="flex items-center space-x-4">
          <!-- Switch back to User Panel -->
          <NuxtLink
            to="/app"
            class="inline-flex items-center text-xs font-semibold px-3 py-1.5 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-200 transition-colors border border-neutral-700"
          >
            <span>← User Panel</span>
          </NuxtLink>

          <div class="text-right hidden sm:block">
            <div class="text-xs font-medium text-neutral-200">{{ user?.displayName }}</div>
            <div class="text-[10px] text-neutral-400">
              {{ (user?.roles ?? []).join(', ') || 'No Role' }}
            </div>
          </div>

          <button
            type="button"
            class="text-xs font-medium text-neutral-400 hover:text-red-400 px-2.5 py-1.5 rounded-md hover:bg-neutral-800 transition-colors"
            @click="logout"
          >
            Sign out
          </button>
        </div>
      </div>
    </header>

    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex-1 w-full py-8">
      <div class="grid grid-cols-1 md:grid-cols-4 gap-6">
        <!-- Sidebar Navigation -->
        <aside class="md:col-span-1 space-y-1">
          <nav class="space-y-1 bg-neutral-900/90 p-3 rounded-xl border border-neutral-800">
            <NuxtLink
              v-for="link in navItems"
              :key="link.to"
              :to="link.to"
              class="flex items-center px-3 py-2 text-sm font-medium rounded-lg transition-colors"
              :class="[
                $route.path === link.to
                  ? 'bg-amber-500/15 text-amber-300 font-semibold border-l-2 border-amber-500'
                  : 'text-neutral-300 hover:bg-neutral-800 hover:text-white'
              ]"
            >
              {{ link.label }}
            </NuxtLink>
          </nav>
        </aside>

        <!-- Main Content Area -->
        <main class="md:col-span-3">
          <slot />
        </main>
      </div>
    </div>
  </div>
</template>
