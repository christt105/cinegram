<template>
  <div class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.7); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px);">
    <div class="glass-panel" style="width: 100%; max-width: 480px; padding: 1.5rem; border-radius: 16px; background: rgba(17,24,39,0.95); border: 1px solid rgba(255,255,255,0.1); display: flex; flex-direction: column; gap: 1rem;">
      <h3 style="margin: 0; font-size: 1.2rem; color: #fff; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">Move {{ fileCount }} file(s) to...</h3>
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
      <div style="display: flex; justify-content: flex-end; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
        <button @click="emit('close')" class="glass-button">Cancel</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { collectionLabel } from '../../utils/format'
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
