<template>
  <div class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px); padding: 1rem;">
    <div class="glass-panel" style="width: 100%; max-width: 600px; max-height: 85vh; display: flex; flex-direction: column; gap: 1rem; padding: 1.5rem; background: rgba(15, 23, 42, 0.95); border: 1px solid rgba(255,255,255,0.08); border-radius: 16px; overflow: hidden; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5);">

      <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">
        <h3 style="margin: 0; font-size: 1.2rem; color: #fff;">Re-identify in TMDB</h3>
        <button @click="emit('close')" class="glass-button icon-only" style="padding: 0; border-radius: 50%; width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-size: 14px;">✕</button>
      </div>

      <div style="display: flex; gap: 0.5rem; margin-bottom: 0.5rem;">
        <input v-model="query" @keyup.enter="search" type="text" placeholder="Name or TMDB ID..." style="flex-grow: 1; padding: 10px 14px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-size: 0.95rem;" />
        <button @click="search" class="glass-button primary" style="padding: 0 1.25rem;">Search</button>
      </div>

      <div style="flex-grow: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 0.75rem; padding-right: 0.25rem; min-height: 200px;">
        <div v-if="searching" style="text-align: center; padding: 2rem; color: #a1a1aa;">
          Searching TMDB...
        </div>
        <div v-else-if="results.length === 0 && query" style="text-align: center; padding: 2rem; color: #a1a1aa;">
          No results found.
        </div>
        <div v-else-if="results.length === 0" style="text-align: center; padding: 2rem; color: #a1a1aa;">
          Type a title above and press Search.
        </div>

        <div v-else v-for="result in results" :key="result.id" class="result-card" style="display: flex; gap: 1rem; padding: 0.75rem; background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.05); border-radius: 10px; transition: background 0.2s;">
          <img v-if="result.poster_path" :src="'https://image.tmdb.org/t/p/w92' + result.poster_path" style="width: 50px; height: 75px; object-fit: cover; border-radius: 6px; flex-shrink: 0;" />
          <div v-else style="width: 50px; height: 75px; background: rgba(255,255,255,0.05); border-radius: 6px; display: flex; align-items: center; justify-content: center; font-size: 1.5rem; color: #4b5563; flex-shrink: 0;">🎬</div>

          <div style="flex-grow: 1; display: flex; flex-direction: column; gap: 0.25rem; min-width: 0;">
            <div style="display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap;">
              <strong style="color: #fff; text-overflow: ellipsis; overflow: hidden; white-space: nowrap; max-width: 250px;">{{ result.title }}</strong>
              <span style="font-size: 0.75rem; background: rgba(255,255,255,0.08); padding: 2px 6px; border-radius: 4px; color: #d1d5db;">{{ result.year }}</span>
              <span style="background: rgba(214, 186, 255, 0.14); color: #d6baff; font-size: 0.7rem; padding: 2px 6px; border-radius: 4px; font-weight: 600; text-transform: uppercase;">
                {{ result.media_type === 'movie' ? 'Movie' : 'Series' }}
              </span>
            </div>
            <p style="margin: 0; font-size: 0.8rem; color: #a1a1aa; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; text-overflow: ellipsis; line-height: 1.4;">{{ result.overview }}</p>
          </div>

          <button @click="emit('select', result.id)" class="glass-button" style="align-self: center; background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff; padding: 6px 12px; font-size: 0.8rem; border-radius: 6px; flex-shrink: 0;">
            Select
          </button>
        </div>
      </div>

      <div style="display: flex; justify-content: flex-end; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
        <button @click="emit('close')" class="glass-button" style="padding: 6px 16px;">Close</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useTmdbSearch } from '../../composables/useTmdbSearch'

const props = defineProps<{
  type: string
  title: string
}>()

const emit = defineEmits<{
  select: [tmdbId: number]
  close: []
}>()

const { query, results, searching, search } = useTmdbSearch(props.type === 'movies' ? 'movie' : 'tv', props.title)
</script>

<style scoped src="./buttons.css"></style>
