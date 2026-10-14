<template>
  <Transition name="slide-up">
    <div v-if="open" class="selection-bar">
      <span class="selection-count">{{ selectedCount }} selected</span>
      <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
        <button @click="emit('delete')" class="glass-button danger btn-sm" :disabled="selectedCount === 0">
          <Trash2 :size="14" /> Delete
        </button>
        <button @click="emit('move')" class="glass-button btn-sm" :disabled="selectedCount === 0 || !canMove" style="background: rgba(214,186,255,0.14); border-color: rgba(214,186,255,0.30); color: #d6baff;">
          Move to...
        </button>
        <button @click="emit('new-collection')" class="glass-button btn-sm" :disabled="selectedCount === 0" style="background: rgba(74,222,128,0.14); border-color: rgba(74,222,128,0.30); color: #4ade80;">
          New collection
        </button>
        <button @click="emit('cancel')" class="glass-button btn-sm">
          Cancel
        </button>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { Trash2 } from 'lucide-vue-next'

defineProps<{
  open: boolean
  selectedCount: number
  canMove: boolean
}>()

const emit = defineEmits<{
  delete: []
  move: []
  'new-collection': []
  cancel: []
}>()
</script>

<style scoped src="./buttons.css"></style>
<style scoped>
.selection-bar {
  position: fixed;
  bottom: 1.5rem;
  left: 50%;
  transform: translateX(-50%);
  background: rgba(17, 24, 39, 0.95);
  border: 1px solid rgba(255,255,255,0.12);
  border-radius: 14px;
  padding: 0.75rem 1.25rem;
  display: flex;
  align-items: center;
  gap: 1rem;
  box-shadow: 0 8px 32px rgba(0,0,0,0.5);
  backdrop-filter: blur(12px);
  z-index: 500;
  white-space: nowrap;
}

.selection-count {
  font-weight: 600;
  color: #fff;
  font-size: 0.9rem;
  min-width: 80px;
}

.slide-up-enter-active, .slide-up-leave-active {
  transition: all 0.2s ease;
}
.slide-up-enter-from, .slide-up-leave-to {
  opacity: 0;
  transform: translateX(-50%) translateY(12px);
}
</style>
