<script setup lang="ts">
// Auth layout for centered public auth screens
</script>

<template>
  <div class="min-h-screen bg-neutral-50 dark:bg-neutral-950 flex flex-col justify-center py-12 sm:px-6 lg:px-8 text-neutral-900 dark:text-neutral-100">
    <div class="sm:mx-auto sm:w-full sm:max-w-md text-center mb-6">
      <div class="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-emerald-600 text-white font-bold text-2xl shadow-md mb-3">
        ID
      </div>
      <h1 class="text-2xl font-bold tracking-tight">Identity Reference Architecture</h1>
      <p class="text-xs text-neutral-500 dark:text-neutral-400 mt-1">
        ASP.NET Core 10 Identity • ES256 JWT • Nuxt 4 BFF
      </p>
    </div>

    <div class="sm:mx-auto sm:w-full sm:max-w-md px-4 sm:px-0">
      <div class="bg-white dark:bg-neutral-900 py-8 px-6 shadow-sm ring-1 ring-neutral-200 dark:ring-neutral-800 sm:rounded-xl">
        <slot />
      </div>
    </div>
  </div>
</template>
