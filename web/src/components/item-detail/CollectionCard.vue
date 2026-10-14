<template>
  <div class="collection-item glass-panel">
    <div class="col-info">
      <div class="col-title-row">
        <strong class="col-name">{{ collection.name || collection.quality || (seasonPack ? 'Full Season' : 'Full Backup') }}</strong>
        <span v-if="collection.files && collection.files.length" class="files-chip label-caps">
          {{ collection.files.length }} {{ collection.files.length === 1 ? 'file' : 'files' }}
        </span>
      </div>
      <span v-if="!seasonPack && collection.quality && collection.name" class="col-meta">Quality: {{ collection.quality }}</span>
      <span v-if="collection.audio_languages" class="col-meta">Audio: {{ collection.audio_languages }}</span>
      <span v-if="techSummary" class="col-meta">{{ techSummary }}</span>
      <span v-if="collection.local_path" class="col-meta" style="color: #4ade80;" :title="collection.local_path">
        ⬇ On disk: {{ shortPath(collection.local_path) }}
      </span>

      <ul v-if="collection.files && collection.files.length" class="file-list">
        <li v-for="f in collection.files" :key="f.id"
            :class="{ 'file-selected': selecting && selectedFileIds.includes(f.id) }"
            :style="selecting ? { cursor: 'pointer' } : {}"
            @click="selecting ? emit('toggle-file', f.id) : null">
          <input v-if="selecting" type="checkbox" :checked="selectedFileIds.includes(f.id)" @click.stop @change="emit('toggle-file', f.id)" style="flex-shrink:0;" />
          <span class="file-name">{{ f.filename }}</span>
          <span class="file-date">{{ formatDate(f.created_at) }}</span>
          <span class="file-size">{{ formatSize(f.filesize) }}</span>
        </li>
      </ul>
      <span v-else-if="!seasonPack" class="col-meta muted">No file details available.</span>
    </div>
    <div class="col-actions">
      <button @click="emit('toggle-select')" class="glass-button" :class="{ 'btn-sm': seasonPack }" :style="selecting ? 'background: rgba(74,222,128,0.14); border-color: rgba(74,222,128,0.30); color: #4ade80;' : 'background: rgba(255,255,255,0.06); border-color: rgba(255,255,255,0.12); color: #a1a1aa;'">
        {{ selecting ? 'Cancel' : 'Select' }}
      </button>
      <template v-if="!selecting">
        <button @click="sendCollectionPreview(collection.id)" class="glass-button" :class="{ 'btn-sm': seasonPack }" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;" :disabled="sendingPreview === collection.id" :title="seasonPack ? 'Send to Telegram' : 'Send Info & Files to Telegram'">
          <span>{{ sendingPreview === collection.id ? '⏳' : '📨' }}</span>
          <span v-if="!seasonPack" style="margin-left: 4px;">{{ sendingPreview === collection.id ? 'Sending...' : 'Send' }}</span>
        </button>
        <button @click="emit('download')" class="glass-button primary" :class="{ 'btn-sm': seasonPack }">
          <DownloadCloud :size="iconSize" /> {{ seasonPack ? 'Download Pack' : 'Download' }}
        </button>
        <button @click="emit('probe')" class="glass-button" :class="{ 'btn-sm': seasonPack }" style="background: rgba(96, 165, 250, 0.14); border-color: rgba(96, 165, 250, 0.30); color: #60a5fa;" title="Read technical data from a file already on disk">
          <FileSearch :size="iconSize" /> {{ seasonPack ? 'Probe' : 'Probe file' }}
        </button>
        <button v-if="collection.local_path" @click="deleteLocalCopy(collection)" class="glass-button" :class="{ 'btn-sm': seasonPack }" :disabled="deletingLocalCopy === collection.id" style="background: rgba(251, 146, 60, 0.14); border-color: rgba(251, 146, 60, 0.30); color: #fb923c;" title="Delete the file from disk, keeping the Telegram collection">
          <HardDrive :size="iconSize" /> {{ deletingLocalCopy === collection.id ? (seasonPack ? '…' : 'Deleting…') : (seasonPack ? 'Local' : 'Delete local') }}
        </button>
        <button @click="emit('reidentify')" class="glass-button" :class="{ 'btn-sm': seasonPack }" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;">
          <Search :size="iconSize" /> {{ seasonPack ? 'Re-id' : 'Re-identify' }}
        </button>
        <button @click="emit('edit')" class="glass-button" :class="{ 'btn-sm': seasonPack }" style="background: rgba(255, 255, 255, 0.08); border-color: rgba(255, 255, 255, 0.15); color: #fff;">
          <Edit3 :size="iconSize" /> Edit
        </button>
        <button @click="deleteCollection(collection.id)" class="glass-button danger" :class="{ 'btn-sm': seasonPack }">
          <Trash2 :size="iconSize" /> Delete
        </button>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { DownloadCloud, Trash2, Edit3, Search, FileSearch, HardDrive } from 'lucide-vue-next'
import { formatDate, formatSize, shortPath, technicalSummary } from '../../utils/format'
import { useCollectionActions } from '../../composables/useCollectionActions'
import type { TelegramCollection } from '../../api/cinegram'

const props = defineProps<{
  collection: TelegramCollection
  seasonPack?: boolean
  selecting: boolean
  selectedFileIds: number[]
}>()

const emit = defineEmits<{
  'toggle-select': []
  'toggle-file': [fileId: number]
  download: []
  probe: []
  reidentify: []
  edit: []
  changed: []
}>()

const { sendingPreview, deletingLocalCopy, sendCollectionPreview, deleteLocalCopy, deleteCollection } =
  useCollectionActions(() => emit('changed'))

const iconSize = computed(() => props.seasonPack ? 14 : 16)
const techSummary = computed(() => technicalSummary(props.collection.technical_metadata))
</script>

<style scoped src="./buttons.css"></style>
<style scoped src="./collection-item.css"></style>
<style scoped>
.col-info {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 0;
}
.col-title-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}
.col-name {
  font-size: 1.1rem;
  font-weight: 700;
  color: var(--on-surface);
}
.files-chip {
  padding: 3px 8px;
  border-radius: var(--r-full);
  background: rgba(34, 197, 94, 0.14);
  color: #7ee2a8;
  font-size: 10px;
}
.col-meta {
  font-size: 0.85rem;
  color: var(--on-surface-variant);
}
.col-meta.muted {
  opacity: 0.6;
  font-style: italic;
}
.file-list {
  list-style: none;
  margin: 0.25rem 0 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  border-top: 1px solid var(--glass-border);
}
.file-list li {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.75rem;
  padding: 0.4rem 0;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  font-size: 0.8rem;
}
.file-name {
  color: var(--on-surface);
  overflow-wrap: anywhere;
  min-width: 0;
}
.file-date {
  color: var(--on-surface-variant);
  font-size: 0.75rem;
  white-space: nowrap;
  flex-shrink: 0;
  opacity: 0.6;
}
.file-size {
  color: var(--on-surface-variant);
  font-family: 'Geist', 'Inter', monospace;
  white-space: nowrap;
  flex-shrink: 0;
}
.file-selected {
  background: rgba(74, 222, 128, 0.08);
}
.file-list li:has(input[type="checkbox"]) {
  gap: 0.5rem;
}
.col-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  align-items: center;
}

@media (max-width: 768px) {
  .col-actions {
    width: 100%;
    justify-content: flex-start !important;
  }
}
</style>
