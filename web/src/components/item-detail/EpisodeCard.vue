<template>
  <div class="episode-item glass-panel" style="flex-direction: column; align-items: stretch; gap: 0;">
    <div class="ep-info" style="border-bottom: 1px solid rgba(255,255,255,0.1); padding-bottom: 0.75rem; margin-bottom: 0.75rem;">
      <strong style="color: var(--jellyfin-blue);">E{{ episode.episode_number }}</strong>
      <input v-if="editableTitle" :value="episode.title" @change="emit('rename', ($event.target as HTMLInputElement).value)" type="text" placeholder="Episode title" style="width: 60%; padding: 4px 8px; border-radius: 6px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-weight: 500;" />
      <span v-else style="font-weight: 500;">{{ episode.title?.replace(/^Episode\s+\d+$/, '') || 'Episode ' + episode.episode_number }}</span>
    </div>

    <div v-if="episode.collections && episode.collections.length > 0" class="ep-collections" style="width: 100%;">
      <div v-for="col in episode.collections" :key="col.id" class="collection-item" style="background: rgba(0,0,0,0.2); padding: 0.5rem; border-radius: 8px; margin-bottom: 0.5rem; border: 1px solid rgba(255,255,255,0.03);">
        <div style="display: flex; flex-direction: column;">
          <span style="font-size: 0.85rem; color: #d1d5db; font-weight: 500;">{{ col.name || col.quality || 'Auto' }}</span>
          <span style="font-size: 0.75rem; color: #888;">{{ col.files?.length || 0 }} files</span>
          <span v-if="col.local_path" style="font-size: 0.75rem; color: #4ade80;" :title="col.local_path">⬇ On disk</span>
        </div>
        <div style="display: flex; gap: 0.25rem; flex-wrap: wrap; justify-content: flex-end;">
          <button @click="emit('download', col)" class="glass-button primary btn-sm icon-only" title="Download">
            <DownloadCloud :size="14" />
          </button>
          <button @click="emit('probe', col)" class="glass-button btn-sm icon-only" style="background: rgba(96, 165, 250, 0.14); border-color: rgba(96, 165, 250, 0.30); color: #60a5fa;" title="Read technical data from a file already on disk">
            <FileSearch :size="14" />
          </button>
          <button v-if="col.local_path" @click="deleteLocalCopy(col)" :disabled="deletingLocalCopy === col.id" class="glass-button btn-sm icon-only" style="background: rgba(251, 146, 60, 0.14); border-color: rgba(251, 146, 60, 0.30); color: #fb923c;" title="Delete the file from disk, keeping the Telegram collection">
            <HardDrive :size="14" />
          </button>
          <button @click="emit('reidentify', col)" class="glass-button btn-sm icon-only" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;" title="Re-identify">
            <Search :size="14" />
          </button>
          <button @click="emit('edit', col)" class="glass-button btn-sm icon-only" style="background: rgba(255, 255, 255, 0.08); border-color: rgba(255, 255, 255, 0.15); color: #fff;" title="Edit">
            <Edit3 :size="14" />
          </button>
          <button @click="deleteCollection(col.id)" class="glass-button danger btn-sm icon-only" title="Delete">
            <Trash2 :size="14" />
          </button>
        </div>
      </div>
    </div>
    <div v-else class="no-col" style="color: var(--text-secondary); font-size: 0.85rem;">Not backed up yet</div>
  </div>
</template>

<script setup lang="ts">
import { DownloadCloud, Trash2, Edit3, Search, FileSearch, HardDrive } from 'lucide-vue-next'
import { useCollectionActions } from '../../composables/useCollectionActions'
import type { CatalogEpisode, TelegramCollection } from '../../api/cinegram'

defineProps<{
  episode: CatalogEpisode
  editableTitle: boolean
}>()

const emit = defineEmits<{
  rename: [title: string]
  download: [collection: TelegramCollection]
  probe: [collection: TelegramCollection]
  reidentify: [collection: TelegramCollection]
  edit: [collection: TelegramCollection]
  changed: []
}>()

const { deletingLocalCopy, deleteLocalCopy, deleteCollection } = useCollectionActions(() => emit('changed'))
</script>

<style scoped src="./buttons.css"></style>
<style scoped src="./collection-item.css"></style>
<style scoped>
.episode-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  flex-wrap: wrap;
  gap: 0.5rem;
}
.ep-info {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  flex: 1 1 auto;
}
.ep-collections {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
}
</style>
