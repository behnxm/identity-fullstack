<script setup lang="ts">
definePageMeta({
  auth: false
})

const { loggedIn, user } = useUserSession()
const { can, resolveLandingPath } = useCapabilities()

const destination = computed(() => {
  if (!loggedIn.value) return '/login'
  return resolveLandingPath()
})
</script>

<template>
  <div class="min-h-screen bg-neutral-950 text-neutral-100 flex flex-col">
    <!-- Header -->
    <header class="border-b border-neutral-800 bg-neutral-900/60 backdrop-blur sticky top-0 z-30">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        <div class="flex items-center space-x-3">
          <div class="w-8 h-8 rounded-lg bg-emerald-500 text-neutral-950 flex items-center justify-center font-black text-sm">
            ID
          </div>
          <span class="font-bold text-lg text-white">Identity Fullstack</span>
          <span class="text-xs px-2 py-0.5 rounded bg-neutral-800 text-neutral-400 font-mono">
            .NET 10 + Nuxt 4 BFF
          </span>
        </div>

        <div class="flex items-center space-x-3">
          <NuxtLink
            v-if="!loggedIn"
            to="/login"
            class="text-sm font-medium px-4 py-2 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-white transition-colors"
          >
            Sign in
          </NuxtLink>
          <NuxtLink
            v-if="!loggedIn"
            to="/register"
            class="text-sm font-medium px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors font-semibold"
          >
            Create account
          </NuxtLink>
          <NuxtLink
            v-if="loggedIn"
            :to="destination"
            class="text-sm font-medium px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors font-semibold"
          >
            Go to {{ can('panel.admin') ? 'Admin Console' : 'User Panel' }} →
          </NuxtLink>
        </div>
      </div>
    </header>

    <!-- Hero -->
    <main class="flex-1 max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-16 flex flex-col justify-center">
      <div class="text-center max-w-3xl mx-auto">
        <div class="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-emerald-950/60 border border-emerald-800/80 text-emerald-300 text-xs font-medium mb-6">
          <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
          Production Reference Architecture
        </div>

        <h1 class="text-4xl sm:text-6xl font-extrabold tracking-tight text-white mb-6">
          Zero-Trust Identity with <span class="text-transparent bg-clip-text bg-gradient-to-r from-emerald-400 to-teal-300">ASP.NET Core</span> and <span class="text-transparent bg-clip-text bg-gradient-to-r from-teal-300 to-sky-400">Nuxt 4</span>
        </h1>

        <p class="text-lg text-neutral-400 mb-10 leading-relaxed">
          Backend-for-Frontend (BFF) reference implementation. The browser never sees a JWT or refresh token.
          Signed ES256 access tokens and rotating refresh tokens stay sealed inside encrypted httpOnly session cookies.
          Server-evaluated policy decisions govern panel routing.
        </p>

        <div class="flex flex-wrap items-center justify-center gap-4">
          <NuxtLink
            v-if="!loggedIn"
            to="/login"
            class="px-6 py-3 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-base transition-colors shadow-lg shadow-emerald-900/30"
          >
            Explore the Demo
          </NuxtLink>
          <NuxtLink
            v-else
            :to="destination"
            class="px-6 py-3 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-base transition-colors shadow-lg shadow-emerald-900/30"
          >
            Launch {{ can('panel.admin') ? 'Admin Console' : 'User Panel' }}
          </NuxtLink>
          <a
            href="https://github.com"
            target="_blank"
            rel="noopener noreferrer"
            class="px-6 py-3 rounded-xl bg-neutral-900 hover:bg-neutral-800 text-neutral-300 border border-neutral-800 font-semibold text-base transition-colors"
          >
            Source Architecture
          </a>
        </div>
      </div>

      <!-- Feature Grid -->
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mt-20">
        <div class="p-6 rounded-2xl bg-neutral-900/70 border border-neutral-800">
          <div class="w-10 h-10 rounded-xl bg-emerald-950/80 border border-emerald-800/80 flex items-center justify-center text-emerald-400 mb-4 font-mono font-bold">
            ES
          </div>
          <h3 class="text-lg font-semibold text-white mb-2">ES256 & Key Rotation</h3>
          <p class="text-sm text-neutral-400">
            Asymmetric ECDSA P-256 tokens validated against standard JWKS endpoints with proactive key rollover and dual-key grace periods.
          </p>
        </div>

        <div class="p-6 rounded-2xl bg-neutral-900/70 border border-neutral-800">
          <div class="w-10 h-10 rounded-xl bg-teal-950/80 border border-teal-800/80 flex items-center justify-center text-teal-400 mb-4 font-mono font-bold">
            BFF
          </div>
          <h3 class="text-lg font-semibold text-white mb-2">BFF Single-Flight Mutex</h3>
          <p class="text-sm text-neutral-400">
            Nuxt server route holds rotating refresh tokens in sealed session cookies. Proactive refresh and single-flight deduplication prevent token reuse races.
          </p>
        </div>

        <div class="p-6 rounded-2xl bg-neutral-900/70 border border-neutral-800">
          <div class="w-10 h-10 rounded-xl bg-sky-950/80 border border-sky-800/80 flex items-center justify-center text-sky-400 mb-4 font-mono font-bold">
            POL
          </div>
          <h3 class="text-lg font-semibold text-white mb-2">Policy-Driven Panels</h3>
          <p class="text-sm text-neutral-400">
            Roles are strictly permission bundles. Access to User Panel (<code class="text-emerald-400">panel.user</code>) and Admin Console (<code class="text-amber-400">panel.admin</code>) is decided by the API's policy engine.
          </p>
        </div>
      </div>
    </main>
  </div>
</template>
