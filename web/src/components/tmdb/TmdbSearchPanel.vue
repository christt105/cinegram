<template>
  <div class="tmdb-search">
    <div class="search-row">
      <input v-model="query" type="text" placeholder="Name or TMDB ID..." class="search-input" @keyup.enter="search" />
      <button class="glass-button primary search-button" @click="search">Search</button>
    </div>

    <div class="search-results">
      <div v-if="searching" class="search-placeholder">Searching TMDB...</div>
      <div v-else-if="searched && results.length === 0" class="search-placeholder">No results found.</div>
      <div v-else-if="results.length === 0" class="search-placeholder">Type a title above and press Search.</div>

      <div v-else v-for="result in results" :key="result.id" class="result-card">
        <img v-if="result.poster_path" :src="'https://image.tmdb.org/t/p/w92' + result.poster_path" class="result-poster" />
        <div v-else class="result-poster result-poster-placeholder">🎬</div>

        <div class="result-info">
          <div class="result-title-row">
            <strong>{{ result.title }}</strong>
            <span class="result-year">{{ result.year }}</span>
            <span class="result-type" :class="result.media_type">{{ result.media_type === 'movie' ? 'Movie' : 'Series' }}</span>
          </div>
          <p class="result-overview">{{ result.overview }}</p>
        </div>

        <button class="glass-button select-button" :disabled="selectDisabled" @click="emit('select', result)">{{ selectLabel ?? 'Select' }}</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useTmdbSearch, type TmdbSearchResult } from '../../composables/useTmdbSearch'

/** Search box and result list over TMDB; emits the picked result. */
const props = defineProps<{
  mediaType: 'movie' | 'tv' | 'multi'
  initialQuery: string
  selectDisabled?: boolean
  selectLabel?: string
  searchOnMount?: boolean
}>()

const emit = defineEmits<{
  select: [result: TmdbSearchResult]
}>()

const { query, results, searching, searched, search } = useTmdbSearch(props.mediaType, props.initialQuery)

onMounted(() => {
  if (props.searchOnMount) search()
})
</script>

<style scoped>
.tmdb-search {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  flex-grow: 1;
  min-height: 0;
}

.search-row {
  display: flex;
  gap: 0.5rem;
}

.search-input {
  flex-grow: 1;
  min-width: 0;
  padding: 10px 14px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.12);
  color: #fff;
  font-size: 0.95rem;
}

.search-button {
  padding: 0 1.25rem;
  flex-shrink: 0;
}

.search-results {
  flex-grow: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding-right: 0.25rem;
  min-height: 150px;
}

.search-placeholder {
  text-align: center;
  padding: 2rem;
  color: #a1a1aa;
}

.result-card {
  display: flex;
  gap: 1rem;
  padding: 0.75rem;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.05);
  border-radius: 10px;
  transition: background 0.2s;
}

.result-poster {
  width: 50px;
  height: 75px;
  object-fit: cover;
  border-radius: 6px;
  flex-shrink: 0;
}

.result-poster-placeholder {
  background: rgba(255, 255, 255, 0.05);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  color: #4b5563;
}

.result-info {
  flex-grow: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
}

.result-title-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.result-title-row strong {
  color: #fff;
  text-overflow: ellipsis;
  overflow: hidden;
  white-space: nowrap;
  max-width: 250px;
}

.result-year {
  font-size: 0.75rem;
  background: rgba(255, 255, 255, 0.08);
  padding: 2px 6px;
  border-radius: 4px;
  color: #d1d5db;
}

.result-type {
  font-size: 0.7rem;
  padding: 2px 6px;
  border-radius: 4px;
  font-weight: 600;
  text-transform: uppercase;
  background: rgba(214, 186, 255, 0.14);
  color: var(--secondary);
}

.result-type.tv {
  background: rgba(74, 222, 128, 0.14);
  color: #4ade80;
}

.result-overview {
  margin: 0;
  font-size: 0.8rem;
  color: #a1a1aa;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.select-button {
  align-self: center;
  background: rgba(214, 186, 255, 0.14);
  border-color: rgba(214, 186, 255, 0.3);
  color: var(--secondary);
  padding: 6px 12px;
  font-size: 0.8rem;
  border-radius: 6px;
  flex-shrink: 0;
}
</style>
