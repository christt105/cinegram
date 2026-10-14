<template>
  <div class="season-block">
    <div class="season-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid rgba(255,255,255,0.1); flex-wrap: wrap; gap: 1rem; cursor: pointer;" @click="emit('toggle-expanded')">
      <div style="display: flex; align-items: center; gap: 0.5rem;">
        <ChevronDown v-if="expanded" :size="20" style="flex-shrink: 0; color: #a1a1aa;" />
        <ChevronRight v-else :size="20" style="flex-shrink: 0; color: #a1a1aa;" />
        <h3 style="font-size: 1.5rem; margin: 0;">Season {{ season.season_number }}</h3>
      </div>
      <div style="display: flex; gap: 1rem; flex-wrap: wrap; align-items: center;" @click.stop>
        <label style="display: flex; align-items: center; gap: 0.4rem; font-size: 0.8rem; color: #d1d5db; cursor: pointer;" title="Non-official numbering: cinegram writes local .nfo files for this season instead of relying on Jellyfin's online providers">
          <input type="checkbox" :checked="season.local_metadata" @change="toggleLocalMetadata" style="width: 15px; height: 15px; cursor: pointer; accent-color: var(--jellyfin-blue);" />
          Local metadata (non-official)
        </label>
        <div style="display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center;">
          <button @click="sendSeasonPreview" class="glass-button" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;" :disabled="sendingPreview" :title="'Send season Info & Files to Telegram'">
            <span v-if="sendingPreview">⏳</span>
            <span v-else>📨</span>
            <span style="margin-left: 4px;">{{ sendingPreview ? 'Sending...' : 'Send' }}</span>
          </button>
          <button @click="downloadSeason" class="glass-button primary">
            <DownloadCloud :size="16" /> Download Season
          </button>
        </div>
      </div>
    </div>

    <div v-show="expanded">
      <div v-if="season.collections && season.collections.length > 0" class="season-packs-section" style="margin-bottom: 1.5rem;">
        <h4 style="margin: 0 0 0.75rem 0; font-size: 1rem; color: #4ade80; display: flex; align-items: center; gap: 0.5rem;">
          📦 Full Season Packs
        </h4>
        <div class="season-packs-list" style="display: flex; flex-direction: column; gap: 0.75rem;">
          <CollectionCard
            v-for="col in season.collections"
            :key="col.id"
            :collection="col"
            season-pack
            :selecting="selectingCollectionId === col.id"
            :selected-file-ids="selectedFileIds"
            @toggle-select="emit('toggle-select', col.id)"
            @toggle-file="emit('toggle-file', $event)"
            @download="emit('download', col)"
            @probe="emit('probe', col)"
            @reidentify="emit('reidentify', col)"
            @edit="emit('edit', col, season.season_number, null)"
            @changed="emit('changed')"
          />
        </div>
      </div>

      <div class="episode-list">
        <EpisodeCard
          v-for="ep in season.episodes"
          :key="ep.id"
          :episode="ep"
          :editable-title="season.local_metadata"
          @rename="updateEpisodeTitle(ep, $event)"
          @download="emit('download', $event)"
          @probe="emit('probe', $event)"
          @reidentify="emit('reidentify', $event)"
          @edit="emit('edit', $event, season.season_number, ep.episode_number)"
          @changed="emit('changed')"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { DownloadCloud, ChevronDown, ChevronRight } from 'lucide-vue-next'
import { apiUrl } from '../../config'
import CollectionCard from './CollectionCard.vue'
import EpisodeCard from './EpisodeCard.vue'

const props = defineProps<{
  seriesId: string
  season: any
  expanded: boolean
  selectingCollectionId: number | null
  selectedFileIds: number[]
}>()

const emit = defineEmits<{
  'toggle-expanded': []
  'toggle-select': [collectionId: number]
  'toggle-file': [fileId: number]
  download: [collection: any]
  probe: [collection: any]
  reidentify: [collection: any]
  edit: [collection: any, season: number, episode: number | null]
  changed: []
}>()

const seasonPath = () => `${apiUrl}/series/${props.seriesId}/seasons/${props.season.season_number}`

const toggleLocalMetadata = async () => {
  const season = props.season
  const previous = season.local_metadata
  season.local_metadata = !previous
  try {
    const res = await fetch(seasonPath(), {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ local_metadata: season.local_metadata })
    })
    if (!res.ok) throw new Error('Request failed')
  } catch (err) {
    console.error(err)
    season.local_metadata = previous
    alert("Error updating season.")
  }
}

const updateEpisodeTitle = async (ep: any, newTitle: string) => {
  const previous = ep.title
  ep.title = newTitle
  try {
    const res = await fetch(`${seasonPath()}/episodes/${ep.episode_number}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title: newTitle })
    })
    if (!res.ok) throw new Error('Request failed')
  } catch (err) {
    console.error(err)
    ep.title = previous
    alert("Error updating episode title.")
  }
}

const sendingPreview = ref(false)

const sendSeasonPreview = async () => {
  sendingPreview.value = true
  try {
    const res = await fetch(`${apiUrl}/preview/series/${props.seriesId}/season/${props.season.season_number}`, { method: 'POST' })
    if (res.ok) {
      alert('✅ Season preview sent to Telegram!')
    } else {
      const err = await res.text()
      alert('❌ Error sending season preview: ' + err)
    }
  } catch (err) {
    console.error(err)
    alert('❌ Connection error.')
  } finally {
    sendingPreview.value = false
  }
}

const downloadSeason = async () => {
  try {
    const res = await fetch(`${apiUrl}/downloads/enqueue/series/${props.seriesId}/season/${props.season.season_number}`, { method: 'POST' })
    if (res.ok) {
      alert("Full season added to queue.")
    } else {
      alert("Error downloading season.")
    }
  } catch (err) {
    console.error(err)
  }
}
</script>

<style scoped>
.season-block {
  margin-bottom: 2.5rem;
}
.episode-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 0.75rem;
}

@media (max-width: 768px) {
  .episode-list {
    grid-template-columns: 1fr;
  }
}
</style>
