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

const route = useRoute()
const router = useRouter()
const { proxyGet, proxyPut, proxyDelete } = useBff()

const id = computed(() => route.params.id as string)
const note = ref<NoteResponse | null>(null)
const title = ref('')
const content = ref('')
const loading = ref(true)
const saving = ref(false)
const deleting = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

const loadNote = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    const data = await proxyGet<NoteResponse>(`/notes/${id.value}`)
    note.value = data
    title.value = data.title
    content.value = data.content
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to retrieve note.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadNote()
})

const handleSave = async () => {
  if (!title.value.trim()) return
  saving.value = true
  errorMessage.value = null
  successMessage.value = null
  try {
    const updated = await proxyPut<NoteResponse>(`/notes/${id.value}`, {
      title: title.value.trim(),
      content: content.value.trim()
    })
    note.value = updated
    successMessage.value = 'Note updated successfully.'
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to update note.'
  } finally {
    saving.value = false
  }
}

const handleDelete = async () => {
  if (!confirm('Are you sure you want to delete this note?')) return
  deleting.value = true
  errorMessage.value = null
  try {
    await proxyDelete(`/notes/${id.value}`)
    await router.push('/app/notes')
  } catch (err: any) {
    errorMessage.value = err?.data?.detail || err?.data?.title || err?.message || 'Failed to delete note.'
    deleting.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <div>
        <NuxtLink
          to="/app/notes"
          class="text-xs text-neutral-500 hover:text-neutral-700 dark:hover:text-neutral-300 font-medium inline-flex items-center gap-1 mb-1"
        >
          ← Back to Notes
        </NuxtLink>
        <h1 class="text-2xl font-bold tracking-tight text-neutral-900 dark:text-white">
          {{ loading ? 'Loading Note...' : (note ? note.title : 'Note Details') }}
        </h1>
      </div>

      <button
        v-if="note"
        type="button"
        :disabled="deleting"
        class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-red-600 hover:bg-red-500 text-white transition-colors disabled:opacity-50"
        @click="handleDelete"
      >
        <span v-if="deleting">Deleting...</span>
        <span v-else>Delete Note</span>
      </button>
    </div>

    <div
      v-if="errorMessage"
      class="p-4 rounded-lg bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 font-medium"
      role="alert"
    >
      {{ errorMessage }}
    </div>

    <div
      v-if="successMessage"
      class="p-4 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 text-xs text-emerald-700 dark:text-emerald-300 font-medium"
    >
      {{ successMessage }}
    </div>

    <div v-if="loading" class="py-12 text-center text-xs text-neutral-400">
      Loading note details...
    </div>

    <div v-else-if="note" class="bg-white dark:bg-neutral-900 rounded-2xl p-6 border border-neutral-200 dark:border-neutral-800 shadow-sm space-y-6">
      <form class="space-y-4" @submit.prevent="handleSave">
        <div>
          <label for="title" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
            Note Title
          </label>
          <input
            id="title"
            v-model="title"
            type="text"
            required
            class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
        </div>

        <div>
          <label for="content" class="block text-xs font-semibold text-neutral-700 dark:text-neutral-300 mb-1">
            Note Content
          </label>
          <textarea
            id="content"
            v-model="content"
            rows="8"
            required
            class="w-full px-3 py-2 text-sm rounded-lg border border-neutral-300 dark:border-neutral-700 bg-white dark:bg-neutral-800 text-neutral-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500 font-mono text-xs"
          />
        </div>

        <div class="flex items-center justify-between pt-2">
          <div class="text-[11px] text-neutral-400">
            Last updated: {{ new Date(note.updatedAt).toLocaleString() }}
          </div>

          <button
            type="submit"
            :disabled="saving"
            class="px-4 py-2 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors disabled:opacity-50"
          >
            <span v-if="saving">Saving changes...</span>
            <span v-else>Save Changes</span>
          </button>
        </div>
      </form>

      <!-- Resource-based auth explanation callout -->
      <div class="p-4 rounded-xl bg-neutral-50 dark:bg-neutral-800/60 border border-neutral-200 dark:border-neutral-700 text-xs space-y-1">
        <div class="font-semibold text-neutral-800 dark:text-neutral-200">
          Resource-Based Authorization Context
        </div>
        <p class="text-neutral-500 leading-relaxed text-[11px]">
          This endpoint enforces <code class="text-emerald-600 dark:text-emerald-400 font-mono">NoteOperations.Update</code> via <code class="text-emerald-600 dark:text-emerald-400 font-mono">NoteAuthorizationHandler</code>.
          Only the owner (<code class="font-mono">{{ note.ownerId }}</code>) may update this note.
          Moderators with <code class="text-amber-500 font-mono">notes.moderate</code> and active MFA may read and delete, but cannot edit other users' notes.
        </p>
      </div>
    </div>
  </div>
</template>
