<template>
  <div class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px); padding: 1rem;">
    <div class="glass-panel" style="width: 100%; max-width: 600px; max-height: 85vh; display: flex; flex-direction: column; gap: 1rem; padding: 1.5rem; background: rgba(15, 23, 42, 0.95); border: 1px solid rgba(255,255,255,0.08); border-radius: 16px; overflow: hidden; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5);">

      <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">
        <h3 style="margin: 0; font-size: 1.2rem; color: #fff;">Re-identify in TMDB</h3>
        <button @click="emit('close')" class="glass-button icon-only" style="padding: 0; border-radius: 50%; width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-size: 14px;">✕</button>
      </div>

      <TmdbSearchPanel
        :media-type="type === 'movies' ? 'movie' : 'tv'"
        :initial-query="title"
        @select="emit('select', $event.id)"
      />

      <div style="display: flex; justify-content: flex-end; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
        <button @click="emit('close')" class="glass-button" style="padding: 6px 16px;">Close</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import TmdbSearchPanel from '../tmdb/TmdbSearchPanel.vue'

defineProps<{
  type: string
  title: string
}>()

const emit = defineEmits<{
  select: [tmdbId: number]
  close: []
}>()
</script>

<style scoped src="./buttons.css"></style>
