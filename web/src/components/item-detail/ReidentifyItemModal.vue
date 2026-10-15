<template>
  <BaseModal title="Re-identify in TMDB" @close="emit('close')">
    <TmdbSearchPanel
      :media-type="type === 'movies' ? 'movie' : 'tv'"
      :initial-query="title"
      @select="emit('select', $event.id)"
    />

    <template #footer>
      <button @click="emit('close')" class="glass-button">Close</button>
    </template>
  </BaseModal>
</template>

<script setup lang="ts">
import BaseModal from '../BaseModal.vue'
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
