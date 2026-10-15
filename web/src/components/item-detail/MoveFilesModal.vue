<template>
  <BaseModal :title="`Move ${fileCount} file(s) to...`" max-width="480px" @close="emit('close')">
    <div style="display: flex; flex-direction: column; gap: 0.5rem; max-height: 50vh; overflow-y: auto;">
      <button v-for="col in collections" :key="col.id"
        @click="emit('select', col.id)"
        class="glass-button"
        style="display: flex; justify-content: space-between; align-items: center; padding: 0.75rem 1rem; text-align: left; background: rgba(255,255,255,0.04); border-color: rgba(255,255,255,0.08);">
        <span style="font-weight: 500; color: #fff;">{{ collectionLabel(col) }}</span>
        <span style="font-size: 0.8rem; color: #a1a1aa;">{{ col.files?.length || 0 }} files</span>
      </button>
      <div v-if="collections.length === 0" style="color: #a1a1aa; text-align: center; padding: 1.5rem;">No other collections available.</div>
    </div>
    <template #footer>
      <button @click="emit('close')" class="glass-button">Cancel</button>
    </template>
  </BaseModal>
</template>

<script setup lang="ts">
import { collectionLabel } from '../../utils/format'
import BaseModal from '../BaseModal.vue'
import type { TelegramCollection } from '../../api/cinegram'

defineProps<{
  fileCount: number
  collections: TelegramCollection[]
}>()

const emit = defineEmits<{
  select: [collectionId: number]
  close: []
}>()
</script>
