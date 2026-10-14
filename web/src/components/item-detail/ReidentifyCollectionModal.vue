<template>
  <div class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px); padding: 1rem;">
    <div class="glass-panel" style="width: 100%; max-width: 600px; max-height: 85vh; display: flex; flex-direction: column; gap: 1rem; padding: 1.5rem; background: rgba(15, 23, 42, 0.95); border: 1px solid rgba(214, 186, 255, 0.20); border-radius: 16px; overflow: hidden; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5);">

      <div style="display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">
        <div>
          <h3 style="margin: 0 0 0.25rem 0; font-size: 1.2rem; color: #fff;">🔍 Re-identify Collection</h3>
          <p style="margin: 0; font-size: 0.85rem; color: #d6baff;">{{ collection.name }}</p>
        </div>
        <button @click="emit('close')" class="glass-button icon-only" style="padding: 0; border-radius: 50%; width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-size: 14px; flex-shrink: 0;">✕</button>
      </div>

      <template v-if="step === 'search'">
        <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
          <input v-model="query" @keyup.enter="search" type="text" placeholder="Name or TMDB ID..." style="flex-grow: 1; padding: 10px 14px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-size: 0.95rem;" />
          <button @click="search" class="glass-button primary" style="padding: 0 1.25rem;">Search</button>
        </div>

        <div style="flex-grow: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 0.75rem; padding-right: 0.25rem; min-height: 150px;">
          <div v-if="searching" style="text-align: center; padding: 2rem; color: #a1a1aa;">Searching TMDB...</div>
          <div v-else-if="results.length === 0 && query" style="text-align: center; padding: 2rem; color: #a1a1aa;">No results found.</div>
          <div v-else-if="results.length === 0" style="text-align: center; padding: 1.5rem; color: #a1a1aa;">Type a title above and press Search.</div>

          <div v-else v-for="result in results" :key="result.id" class="result-card" style="display: flex; gap: 1rem; padding: 0.75rem; background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.05); border-radius: 10px; transition: background 0.2s;">
            <img v-if="result.poster_path" :src="'https://image.tmdb.org/t/p/w92' + result.poster_path" style="width: 50px; height: 75px; object-fit: cover; border-radius: 6px; flex-shrink: 0;" />
            <div v-else style="width: 50px; height: 75px; background: rgba(255,255,255,0.05); border-radius: 6px; display: flex; align-items: center; justify-content: center; font-size: 1.5rem; color: #4b5563; flex-shrink: 0;">🎬</div>

            <div style="flex-grow: 1; display: flex; flex-direction: column; gap: 0.25rem; min-width: 0;">
              <div style="display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap;">
                <strong style="color: #fff; text-overflow: ellipsis; overflow: hidden; white-space: nowrap; max-width: 250px;">{{ result.title }}</strong>
                <span style="font-size: 0.75rem; background: rgba(255,255,255,0.08); padding: 2px 6px; border-radius: 4px; color: #d1d5db;">{{ result.year }}</span>
                <span :style="{ background: result.media_type === 'movie' ? 'rgba(214, 186, 255, 0.14)' : 'rgba(74, 222, 128, 0.14)', color: result.media_type === 'movie' ? '#d6baff' : '#4ade80' }" style="font-size: 0.7rem; padding: 2px 6px; border-radius: 4px; font-weight: 600; text-transform: uppercase;">{{ result.media_type === 'movie' ? 'Movie' : 'Series' }}</span>
              </div>
              <p style="margin: 0; font-size: 0.8rem; color: #a1a1aa; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">{{ result.overview }}</p>
            </div>

            <button @click="selectResult(result)" :disabled="loading" class="glass-button" style="align-self: center; background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff; padding: 6px 12px; font-size: 0.8rem; border-radius: 6px; flex-shrink: 0;">
              Select
            </button>
          </div>
        </div>
      </template>

      <template v-else-if="step === 'episode-select'">
        <div style="display: flex; flex-direction: column; gap: 1rem; flex-grow: 1;">
          <p style="margin: 0; font-size: 0.9rem; color: #d1d5db;">
            Linking to series: <strong style="color: #4ade80;">{{ pendingTitle }}</strong>
          </p>
          <p style="margin: 0; font-size: 0.8rem; color: #a1a1aa;">Use season 0 for specials and OVAs.</p>
          <div style="display: flex; gap: 1rem; flex-wrap: wrap;">
            <div style="display: flex; flex-direction: column; gap: 0.4rem; flex: 1; min-width: 120px;">
              <label style="font-size: 0.8rem; color: #a1a1aa; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em;">Season</label>
              <input type="number" v-model.number="seasonNumber" min="0" style="padding: 10px 14px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-size: 1rem; width: 100%; box-sizing: border-box;" />
            </div>
            <div style="display: flex; flex-direction: column; gap: 0.4rem; flex: 1; min-width: 120px;">
              <label style="font-size: 0.8rem; color: #a1a1aa; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em;">Episode <span style="font-weight: 400; text-transform: none;">(empty = season pack)</span></label>
              <input type="number" v-model.number="episodeNumber" min="1" placeholder="—" style="padding: 10px 14px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-size: 1rem; width: 100%; box-sizing: border-box;" />
            </div>
          </div>
        </div>
        <div style="display: flex; gap: 0.5rem; justify-content: flex-end; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
          <button @click="step = 'search'" class="glass-button" style="padding: 6px 16px;">← Back</button>
          <button @click="confirmEpisode" :disabled="loading" class="glass-button" style="background: rgba(74, 222, 128, 0.14); border-color: rgba(74, 222, 128, 0.30); color: #4ade80; padding: 6px 16px;">
            {{ loading ? 'Saving…' : 'Confirm' }}
          </button>
        </div>
      </template>

      <div v-if="step === 'search'" style="display: flex; justify-content: flex-end; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
        <button @click="emit('close')" class="glass-button" style="padding: 6px 16px;">Close</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { apiUrl } from '../../config'
import type { TelegramCollection } from '../../api/cinegram'
import { useTmdbSearch, type TmdbSearchResult } from '../../composables/useTmdbSearch'

const props = defineProps<{ collection: TelegramCollection }>()

const emit = defineEmits<{
  close: []
  reidentified: []
}>()

const { query, results, searching, search } = useTmdbSearch('multi', props.collection.name ?? '')

const step = ref<'search' | 'episode-select'>('search')
const loading = ref(false)
const pendingTmdbId = ref(0)
const pendingTitle = ref('')
const seasonNumber = ref(1)
const episodeNumber = ref<number | null>(null)

const submit = async (body: Record<string, unknown>, errorPrefix: string) => {
  loading.value = true
  try {
    const res = await fetch(`${apiUrl}/collections/${props.collection.id}/reidentify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    })
    if (res.ok) {
      emit('reidentified')
      alert('✅ Collection re-identified successfully.')
    } else {
      alert(errorPrefix + await res.text())
    }
  } catch {
    alert('Connection error.')
  } finally {
    loading.value = false
  }
}

const selectResult = async (result: TmdbSearchResult) => {
  if (result.media_type === 'tv') {
    step.value = 'episode-select'
    pendingTmdbId.value = result.id
    pendingTitle.value = result.title
    seasonNumber.value = 1
    episodeNumber.value = null
  } else {
    await submit({ tmdb_id: result.id, media_type: 'movie' }, '❌ Error re-identifying: ')
  }
}

const confirmEpisode = async () => {
  const body: Record<string, unknown> = {
    tmdb_id: pendingTmdbId.value,
    media_type: 'tv',
    season_number: seasonNumber.value,
  }
  const ep = episodeNumber.value
  if (ep != null && ep >= 1) body.episode_number = ep
  await submit(body, '❌ Error: ')
}
</script>

<style scoped src="./buttons.css"></style>
