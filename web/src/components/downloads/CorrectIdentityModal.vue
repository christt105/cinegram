<template>
  <div class="modal-overlay">
    <div class="glass-panel modal-panel">
      <div class="modal-header">
        <div>
          <h3>Correct identity</h3>
          <p class="modal-subtitle">
            {{ targetRow ? targetRow.filename : `Applying to ${selectedCount} selected file(s)` }}
          </p>
        </div>
        <button class="glass-button icon-only" @click="emit('close')">✕</button>
      </div>

      <template v-if="step === 'search'">
        <div class="search-row">
          <input
            v-model="query"
            type="text"
            placeholder="Name or TMDB ID..."
            class="search-input"
            @keyup.enter="search"
          />
          <button class="glass-button primary" @click="search">Search</button>
        </div>

        <div class="search-results">
          <div v-if="searching" class="search-placeholder">Searching TMDB...</div>
          <div v-else-if="results.length === 0 && query" class="search-placeholder">
            No results found.
          </div>
          <div v-else-if="results.length === 0" class="search-placeholder">
            Type a title above and press Search.
          </div>

          <div v-else v-for="result in results" :key="result.id" class="result-card">
            <img v-if="result.poster_path" :src="'https://image.tmdb.org/t/p/w92' + result.poster_path" class="result-poster" />
            <div v-else class="result-poster result-poster-placeholder">🎬</div>

            <div class="result-info">
              <div class="result-title-row">
                <strong>{{ result.title }}</strong>
                <span class="result-year">{{ result.year }}</span>
                <span class="result-type">{{ result.media_type === 'movie' ? 'Movie' : 'Series' }}</span>
              </div>
              <p class="result-overview">{{ result.overview }}</p>
            </div>

            <button class="glass-button btn-sm" @click="selectResult(result)">Select</button>
          </div>
        </div>
      </template>

      <template v-else-if="step === 'episode-select'">
        <div class="episode-select">
          <p class="modal-subtitle">
            Linking to series: <strong>{{ pendingTitle }}</strong>
          </p>
          <p class="episode-hint">Use season 0 for specials and OVAs.</p>
          <div class="episode-fields">
            <div class="field">
              <label>Season</label>
              <input type="number" v-model.number="season" min="0" />
            </div>
            <div class="field">
              <label>Episode <span class="field-hint">(empty = season pack)</span></label>
              <input type="number" v-model.number="episode" min="1" placeholder="—" />
            </div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="glass-button" @click="step = 'search'">&larr; Back</button>
          <button class="glass-button success" :disabled="saving" @click="applyCorrection">
            {{ saving ? 'Saving…' : 'Confirm' }}
          </button>
        </div>
      </template>

      <div v-if="step === 'search'" class="modal-footer">
        <button class="glass-button" @click="emit('close')">Close</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { correctWatchedFile, type WatchedFile } from '../../api/cinegram'
import { useTmdbSearch, type TmdbSearchResult } from '../../composables/useTmdbSearch'

/** With a target row it corrects that file; without one it hands the picked title back for the selection. */
const props = defineProps<{
  targetRow: WatchedFile | null
  selectedCount: number
}>()

const emit = defineEmits<{
  close: []
  corrected: []
  'batch-select': [result: TmdbSearchResult]
}>()

const { query, results, searching, search } =
  useTmdbSearch('multi', props.targetRow ? props.targetRow.guess_title || props.targetRow.filename : '')

const step = ref<'search' | 'episode-select'>('search')
const pendingTmdbId = ref(0)
const pendingTitle = ref('')
const season = ref(props.targetRow?.guess_season ?? 1)
const episode = ref<number | null>(props.targetRow?.guess_episode ?? null)
const saving = ref(false)

const selectResult = async (result: TmdbSearchResult) => {
  if (!props.targetRow) {
    emit('batch-select', result)
    return
  }

  pendingTmdbId.value = result.id
  pendingTitle.value = result.title
  if (result.media_type === 'tv') {
    step.value = 'episode-select'
  } else {
    await applyCorrection()
  }
}

const applyCorrection = async () => {
  const row = props.targetRow
  if (!row) return
  saving.value = true
  try {
    await correctWatchedFile(row.id, pendingTmdbId.value, season.value, episode.value)
    emit('corrected')
  } catch (err) {
    console.error(err)
    alert(`Failed to correct ${row.filename}.`)
  } finally {
    saving.value = false
  }
}
</script>

<style scoped src="./buttons.css"></style>
<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
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
  max-width: 600px;
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.5rem;
  background: rgba(15, 23, 42, 0.95);
  border: 1px solid rgba(214, 186, 255, 0.2);
  overflow: hidden;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  padding-bottom: 0.75rem;
}

.modal-header h3 {
  margin: 0 0 0.25rem 0;
  font-size: 1.2rem;
  color: #fff;
}

.modal-subtitle {
  margin: 0;
  font-size: 0.85rem;
  color: var(--secondary);
}

.search-row {
  display: flex;
  gap: 0.5rem;
}

.search-input {
  flex-grow: 1;
  padding: 10px 14px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.12);
  color: #fff;
  font-size: 0.95rem;
}

.search-results {
  flex-grow: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding-right: 0.25rem;
  min-height: 200px;
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
  background: rgba(214, 186, 255, 0.14);
  color: var(--secondary);
  padding: 2px 6px;
  border-radius: 4px;
  font-weight: 600;
  text-transform: uppercase;
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

.episode-select {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  flex-grow: 1;
}

.episode-hint {
  margin: 0;
  font-size: 0.8rem;
  color: #a1a1aa;
}

.episode-fields {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  flex: 1;
  min-width: 120px;
}

.field label {
  font-size: 0.8rem;
  color: #a1a1aa;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.field-hint {
  font-weight: 400;
  text-transform: none;
}

.field input {
  padding: 10px 14px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.12);
  color: #fff;
  font-size: 1rem;
  width: 100%;
  box-sizing: border-box;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  padding-top: 0.75rem;
}
</style>
