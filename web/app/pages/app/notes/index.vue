<script setup lang="ts">
definePageMeta({
  layout: 'user'
})

interface NoteResponse {
  id: string
  ownerId: string
  title: string
  content: string
  createdAt: string
  updatedAt: string
}

const { proxyGet, proxyPost } = useBff()

const notes = ref<NoteResponse[]>([])
const loading = ref(true)
const creating = ref(false)
const showModal = ref(false)
const errorMessage = ref<string | null>(null)

const newTitle = ref('')
const newContent = ref('')

const loadNotes = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    notes.value = await proxyGet<NoteResponse[]>('/notes')
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to load notes.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadNotes()
})

const handleCreate = async () => {
  if (!newTitle.value.trim()) return
  creating.value = true
  errorMessage.value = null
  try {
    const created = await proxyPost<NoteResponse>('/notes', {
      title: newTitle.value.trim(),
      content: newContent.value.trim()
    })
    notes.value.unshift(created)
    newTitle.value = ''
    newContent.value = ''
    showModal.value = false
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to create note.'
  } finally {
    creating.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-neutral-900 dark:text-white">
          My Notes
        </h1>
        <p class="text-xs text-neutral-500 mt-0.5">
          Demonstrates resource-based authorization with <code class="font-mono text-emerald-600 dark:text-emerald-400">notes.manage.own</code>.
        </p>
      </div>

      <button
        type="button"
        class="px-4 py-2 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors shadow-sm"
        @click="showModal = true"
      >
        + New Note
      </button>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      class="p-4 rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 font-medium"
      role="alert"
    >
      {{ errorMessage }}
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="py-12 text-center text-xs text-neutral-400">
      Loading personal notes...
    </div>

    <!-- Empty State -->
    <div
      v-else-if="!notes.length"
      class="bg-white dark:bg-neutral-900 rounded-2xl p-12 text-center border border-neutral-200 dark:border-neutral-800"
    >
      <div class="w-12 h-12 mx-auto rounded-full bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 flex items-center justify-center text-emerald-600 dark:text-emerald-300 text-lg mb-3">
        📝
      </div>
      <h3 class="text-base font-semibold text-neutral-900 dark:text-white">No notes found</h3>
      <p class="text-xs text-neutral-500 mt-1 max-w-sm mx-auto">
        Create your first personal note to test CRUD operations and owner-only resource policies.
      </p>
      <button
        type="button"
        class="mt-4 px-4 py-2 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors"
        @click="showModal = true"
      >
        Create Note
      </button>
    </div>

    <!-- Notes Grid -->
    <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <div
        v-for="note in notes"
        :key="note.id"
        class="bg-white dark:bg-neutral-900 rounded-xl p-5 border border-neutral-200 dark:border-neutral-800 flex flex-col justify-between hover:border-emerald-500/50 transition-colors shadow-sm"
      >
        <div>
          <div class="flex items-center justify-between mb-2">
            <h3 class="text-base font-semibold text-neutral-900 dark:text-white line-clamp-1">
              {{ note.title }}
            </h3>
            <span class="text-[10px] text-neutral-400 font-mono">
              {{ new Date(note.updatedAt).toLocaleDateString() }}
            </span>
          </div>
          <p class="text-xs text-neutral-600 dark:text-neutral-400 line-clamp-3 whitespace-pre-line mb-4">
            {{ note.content }}
          </p>
        </div>

        <div class="pt-3 border-t border-neutral-100 dark:border-neutral-800 flex items-center justify-between text-xs">
          <span class="text-[10px] text-neutral-400 font-mono truncate max-w-[150px]">
            ID: {{ note.id }}
          </span>
          <NuxtLink
            :to="`/app/notes/${note.id}`"
            class="text-emerald-600 dark:text-emerald-400 hover:underline font-semibold"
          >
            View / Edit →
          </NuxtLink>
        </div>
      </div>
    </div>

    <!-- Create Modal Backdrop -->
    <div
      v-if="showModal"
      class="fixed inset-0 z-50 bg-neutral-950/70 backdrop-blur-sm flex items-center justify-center p-4"
    >
      <div class="bg-white dark:bg-neutral-900 border border-neutral-200 dark:border-neutral-800 rounded-2xl max-w-lg w-full p-6 shadow-2xl">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-bold text-neutral-900 dark:text-white">
            Create Personal Note
          </h2>
          <button
            type="button"
            class="text-neutral-400 hover:text-neutral-600 dark:hover:text-neutral-200 text-lg leading-none"
            @click="showModal = false"
          >
            ✕
          </button>
        </div>

        <form class="space-y-4" @submit.prevent="handleCreate">
          <div>
            <label for="newTitle" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
              Title
            </label>
            <input
              id="newTitle"
              v-model="newTitle"
              type="text"
              required
              class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
              placeholder="Architecture Discussion Notes"
            >
          </div>

          <div>
            <label for="newContent" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
              Content
            </label>
            <textarea
              id="newContent"
              v-model="newContent"
              rows="4"
              required
              class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
              placeholder="Details on token lifetimes, key rotation grace periods, and BFF proxy rules..."
            />
          </div>

          <div class="flex items-center justify-end space-x-2 pt-2">
            <button
              type="button"
              class="px-3 py-2 text-xs font-medium rounded-lg text-neutral-600 dark:text-neutral-400 hover:bg-neutral-100 dark:hover:bg-neutral-800"
              @click="showModal = false"
            >
              Cancel
            </button>
            <button
              type="submit"
              :disabled="creating || !newTitle.trim()"
              class="px-4 py-2 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors disabled:opacity-50"
            >
              <span v-if="creating">Creating...</span>
              <span v-else>Save Note</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
