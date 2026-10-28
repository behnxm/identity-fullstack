<script setup lang="ts">
const { user } = useUserSession()
const { can } = useCapabilities()
const { logout } = useBff()

const links = [
  { label: 'Overview', to: '/app' },
  { label: 'Notes', to: '/app/notes' },
  { label: 'Profile', to: '/app/profile' },
  { label: 'Security & 2FA', to: '/app/security' }
]
</script>

<template>
  <div class="min-h-screen bg-neutral-100 dark:bg-neutral-950 flex flex-col text-neutral-900 dark:text-neutral-100">
    <!-- Top Bar -->
    <header class="bg-white dark:bg-neutral-900 border-b border-neutral-200 dark:border-neutral-800 sticky top-0 z-30">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        <div class="flex items-center space-x-3">
          <div class="w-8 h-8 rounded-lg bg-emerald-600 text-white flex items-center justify-center font-bold text-sm">
            UP
          </div>
          <span class="font-semibold text-lg">User Panel</span>
          <span class="text-xs px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 font-medium border border-emerald-300 dark:border-emerald-800">
            panel.user
          </span>
        </div>

        <div class="flex items-center space-x-4">
          <!-- Switch to Admin Panel if authorized -->
          <NuxtLink
            v-if="can('panel.admin')"
            to="/admin"
            class="inline-flex items-center text-xs font-semibold px-3 py-1.5 rounded-lg bg-amber-500 hover:bg-amber-600 text-white transition-colors shadow-sm"
          >
            <span>Switch to Admin Console</span>
            <span class="ml-1.5">→</span>
          </NuxtLink>

          <div class="text-right hidden sm:block">
            <div class="text-xs font-medium">{{ user?.displayName }}</div>
            <div class="text-[10px] text-neutral-500">{{ user?.email }}</div>
          </div>

          <button
            type="button"
            class="text-xs font-medium text-neutral-600 dark:text-neutral-400 hover:text-red-600 dark:hover:text-red-400 px-2.5 py-1.5 rounded-md hover:bg-neutral-100 dark:hover:bg-neutral-800 transition-colors"
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
          <nav class="space-y-1 bg-white dark:bg-neutral-900 p-3 rounded-xl border border-neutral-200 dark:border-neutral-800">
            <NuxtLink
              v-for="link in links"
              :key="link.to"
              :to="link.to"
              class="flex items-center px-3 py-2 text-sm font-medium rounded-lg transition-colors"
              :class="[
                $route.path === link.to
                  ? 'bg-emerald-50 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-300 font-semibold'
                  : 'text-neutral-700 dark:text-neutral-300 hover:bg-neutral-50 dark:hover:bg-neutral-800'
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
