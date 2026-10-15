<template>
  <BaseModal title="🔍 Re-identify Collection" :subtitle="collection.name" @close="emit('close')">
    <TmdbSearchPanel
      v-show="step === 'search'"
      media-type="multi"
      :initial-query="collection.name ?? ''"
      :select-disabled="loading"
      @select="selectResult"
    />

    <template v-if="step === 'episode-select'">
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
    </template>

    <template #footer>
      <template v-if="step === 'episode-select'">
        <button @click="step = 'search'" class="glass-button">← Back</button>
        <button @click="confirmEpisode" :disabled="loading" class="glass-button" style="background: rgba(74, 222, 128, 0.14); border-color: rgba(74, 222, 128, 0.30); color: #4ade80;">
          {{ loading ? 'Saving…' : 'Confirm' }}
        </button>
      </template>
      <button v-else @click="emit('close')" class="glass-button">Close</button>
    </template>
  </BaseModal>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { apiUrl } from '../../config'
import type { TelegramCollection } from '../../api/cinegram'
import type { TmdbSearchResult } from '../../composables/useTmdbSearch'
import BaseModal from '../BaseModal.vue'
import TmdbSearchPanel from '../tmdb/TmdbSearchPanel.vue'

const props = defineProps<{ collection: TelegramCollection }>()

const emit = defineEmits<{
  close: []
  reidentified: []
}>()

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
