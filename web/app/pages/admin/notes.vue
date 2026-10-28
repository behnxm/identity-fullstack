<script setup lang="ts">
definePageMeta({
  layout: 'admin',
  policy: 'notes.moderate'
})

interface NoteResponse {
  id: string
  ownerId: string
  title: string
  content: string
  createdAt: string
  updatedAt: string
}

const { proxyGet, proxyDelete } = useBff()

const notes = ref<NoteResponse[]>([])
const loading = ref(true)
const actionLoading = ref(false)
const errorMessage = ref<string | null>(null)

const loadNotes = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    notes.value = await proxyGet<NoteResponse[]>('/admin/notes')
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to load notes for moderation.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadNotes()
})

const handleDelete = async (note: NoteResponse) => {
  if (!confirm(`Moderator Action: Are you sure you want to delete note "${note.title}"?`)) return

  actionLoading.value = true
  try {
    await proxyDelete(`/notes/${note.id}`)
    notes.value = notes.value.filter(n => n.id !== note.id)
    alert('Note removed by moderator.')
  } catch (err: any) {
    alert(err?.data?.detail || 'Failed to delete note.')
  } finally {
    actionLoading.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-white">
          Notes Moderation
        </h1>
        <p class="text-xs text-neutral-400 mt-0.5">
          Global content moderation workspace governed by <code class="text-amber-400 font-mono">notes.moderate</code> policy.
        </p>
      </div>

      <button
        type="button"
        :disabled="loading"
        class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-200 transition-colors border border-neutral-700"
        @click="loadNotes"
      >
        <span v-if="loading">Refreshing...</span>
        <span v-else>↻ Refresh</span>
      </button>
    </div>

    <!-- Architectural Callout -->
    <div class="p-4 rounded-xl bg-neutral-900 border border-neutral-800 text-xs text-neutral-300 space-y-1">
      <div class="font-bold text-amber-400">
        Resource-Based Authorization: Moderator Privileges
      </div>
      <p class="text-[11px] text-neutral-400 leading-relaxed">
        Under <code class="font-mono text-neutral-200">NoteAuthorizationHandler</code>, users possessing the <code class="font-mono text-amber-400">notes.moderate</code> claim and an active MFA AMR claim are permitted to execute <code class="font-mono text-neutral-200">NoteOperations.Read</code> and <code class="font-mono text-neutral-200">NoteOperations.Delete</code> on any note in the database. They are forbidden from updating another user's note.
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
      Loading notes for moderation...
    </div>

    <div v-else-if="!notes.length" class="bg-neutral-900 rounded-2xl p-12 text-center border border-neutral-800">
      <p class="text-xs text-neutral-400">No notes found across the platform.</p>
    </div>

    <div v-else class="space-y-3">
      <div
        v-for="note in notes"
        :key="note.id"
        class="bg-neutral-900 border border-neutral-800 rounded-xl p-5 flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4"
      >
        <div class="space-y-1 flex-1">
          <div class="flex items-center gap-2">
            <h3 class="text-base font-semibold text-white">{{ note.title }}</h3>
            <span class="text-[10px] text-neutral-500 font-mono">
              Owner: {{ note.ownerId }}
            </span>
          </div>
          <p class="text-xs text-neutral-400 whitespace-pre-line line-clamp-3">
            {{ note.content }}
          </p>
          <div class="text-[10px] text-neutral-500 font-mono pt-1">
            Created: {{ new Date(note.createdAt).toLocaleString() }}
          </div>
        </div>

        <button
          type="button"
          :disabled="actionLoading"
          class="shrink-0 px-3 py-1.5 text-xs font-semibold rounded-lg bg-red-600/20 hover:bg-red-600 text-red-400 hover:text-white border border-red-500/40 transition-colors disabled:opacity-50"
          @click="handleDelete(note)"
        >
          Remove Content
        </button>
      </div>
    </div>
  </div>
</template>
