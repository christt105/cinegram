<template>
  <BaseModal :title="`New collection from ${fileCount} file(s)`" max-width="400px" @close="emit('close')">
    <div>
      <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Collection name</label>
      <input v-model="name" @keyup.enter="confirm" type="text" placeholder="e.g. Extended Cut" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
    </div>
    <template #footer>
      <button @click="emit('close')" class="glass-button">Cancel</button>
      <button @click="confirm" class="glass-button" :disabled="!name.trim() || loading" style="background: rgba(74,222,128,0.14); border-color: rgba(74,222,128,0.30); color: #4ade80;">
        {{ loading ? 'Creating...' : 'Create & Move' }}
      </button>
    </template>
  </BaseModal>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import BaseModal from '../BaseModal.vue'

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
