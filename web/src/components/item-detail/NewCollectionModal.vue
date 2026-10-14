<template>
  <div class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.7); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px);">
    <div class="glass-panel" style="width: 100%; max-width: 400px; padding: 1.5rem; border-radius: 16px; background: rgba(17,24,39,0.95); border: 1px solid rgba(255,255,255,0.1); display: flex; flex-direction: column; gap: 1rem;">
      <h3 style="margin: 0; font-size: 1.2rem; color: #fff; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">New collection from {{ fileCount }} file(s)</h3>
      <div>
        <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Collection name</label>
        <input v-model="name" @keyup.enter="confirm" type="text" placeholder="e.g. Extended Cut" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
      </div>
      <div style="display: flex; justify-content: flex-end; gap: 0.75rem; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
        <button @click="emit('close')" class="glass-button">Cancel</button>
        <button @click="confirm" class="glass-button" :disabled="!name.trim() || loading" style="background: rgba(74,222,128,0.14); border-color: rgba(74,222,128,0.30); color: #4ade80;">
          {{ loading ? 'Creating...' : 'Create & Move' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const props = defineProps<{
  fileCount: number
  create: (name: string) => Promise<boolean>
}>()

const emit = defineEmits<{ close: [] }>()

const name = ref('')
const loading = ref(false)

const confirm = async () => {
  if (!name.value.trim()) return
  loading.value = true
  try {
    if (await props.create(name.value.trim())) emit('close')
  } finally {
    loading.value = false
  }
}
</script>
