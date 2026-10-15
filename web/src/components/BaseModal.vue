<template>
  <div class="modal-overlay" @mousedown.self="pressedOnBackdrop = true" @click.self="closeFromBackdrop">
    <div class="modal-panel" role="dialog" aria-modal="true" :aria-labelledby="titleId" :style="{ maxWidth }" @mousedown="pressedOnBackdrop = false">
      <div class="modal-header">
        <div>
          <h3 :id="titleId">{{ title }}</h3>
          <p v-if="subtitle" class="modal-subtitle">{{ subtitle }}</p>
        </div>
        <button class="glass-button modal-close" title="Close" @click="emit('close')">✕</button>
      </div>

      <div class="modal-body">
        <slot />
      </div>

      <div v-if="$slots.footer" class="modal-footer">
        <slot name="footer" />
      </div>
    </div>
  </div>
</template>

<script lang="ts">
const openModals: symbol[] = []
</script>

<script setup lang="ts">
import { ref, useId, onMounted, onBeforeUnmount } from 'vue'

/** Overlay, panel, header with title, subtitle and close button, and a footer slot for the actions. Closes on Escape and on a click on the backdrop. */
withDefaults(defineProps<{
  title: string
  subtitle?: string | null
  maxWidth?: string
}>(), {
  subtitle: null,
  maxWidth: '600px'
})

const emit = defineEmits<{
  close: []
}>()

const titleId = useId()
const pressedOnBackdrop = ref(false)

const closeFromBackdrop = () => {
  if (pressedOnBackdrop.value) emit('close')
  pressedOnBackdrop.value = false
}

const self = Symbol()

const onKeydown = (event: KeyboardEvent) => {
  if (event.key !== 'Escape' || openModals[openModals.length - 1] !== self) return
  event.preventDefault()
  emit('close')
}

onMounted(() => {
  openModals.push(self)
  document.addEventListener('keydown', onKeydown)
})

onBeforeUnmount(() => {
  openModals.splice(openModals.indexOf(self), 1)
  document.removeEventListener('keydown', onKeydown)
})
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.8);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(8px);
  padding: 1rem;
}

.modal-panel {
  width: 100%;
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.5rem;
  background: rgba(17, 24, 39, 0.95);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5);
  overflow: hidden;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  padding-bottom: 0.75rem;
}

.modal-header h3 {
  margin: 0;
  font-size: 1.2rem;
  color: #fff;
}

.modal-subtitle {
  margin: 0.25rem 0 0 0;
  font-size: 0.85rem;
  color: var(--secondary);
}

.modal-close {
  padding: 0;
  border-radius: 50%;
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  flex-shrink: 0;
}

.modal-body {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  flex: 1 1 auto;
  min-height: 0;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  padding-top: 0.75rem;
}
</style>
