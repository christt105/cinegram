<template>
  <div class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px); padding: 1rem;">
    <div class="glass-panel" style="width: 100%; max-width: 680px; padding: 1.5rem; border-radius: 16px; background: rgba(17,24,39,0.95); border: 1px solid rgba(255,255,255,0.1); display: flex; flex-direction: column; gap: 1rem;">
      <div style="display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">
        <div>
          <h3 style="margin: 0; font-size: 1.2rem; color: #fff;">Technical data from a local file</h3>
          <p style="margin: 0.25rem 0 0 0; font-size: 0.85rem; color: #d6baff;">{{ collectionLabel(collection) }}</p>
        </div>
        <button @click="emit('close')" class="glass-button icon-only" style="padding: 0; border-radius: 50%; width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-size: 14px; flex-shrink: 0;">✕</button>
      </div>
      <div style="display: flex; gap: 0.5rem;">
        <input v-model="query" @keyup.enter="searchLocalFiles" type="text" placeholder="Filter by name…" style="flex: 1; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
        <button @click="searchLocalFiles" :disabled="loading" class="glass-button">
          <Search :size="16" />
        </button>
      </div>
      <label v-if="tmdbId || tvdbId" class="probe-scope">
        <input type="checkbox" v-model="onlyThisTitle" @change="searchLocalFiles" />
        Only the copies of this title
      </label>
      <div class="probe-results">
        <div v-if="loading" style="color: #a1a1aa; text-align: center; padding: 1.5rem;">Scanning the library…</div>
        <template v-else>
          <button v-for="file in files" :key="file.path"
            @click="probeLocalFile(file.path)"
            :disabled="submitting"
            class="glass-button probe-file"
            :class="{ 'probe-file-current': file.path === currentPath }">
            <span class="probe-file-name">{{ file.filename }}</span>
            <span class="probe-file-badges">
              <span v-if="file.quality" class="probe-badge quality">{{ file.quality }}</span>
              <span v-if="file.version_tag" class="probe-badge version">{{ file.version_tag }}</span>
              <span class="probe-badge">{{ formatSize(file.filesize) }}</span>
              <span class="probe-badge">{{ formatDate(file.modified_at) }}</span>
              <span v-if="file.path === currentPath" class="probe-badge current">In use</span>
            </span>
            <span class="probe-file-path">{{ folderOf(file.path) }}</span>
          </button>
          <div v-if="files.length === 0" style="color: #a1a1aa; text-align: center; padding: 1.5rem;">
            {{ onlyThisTitle
              ? 'No copies of this title on disk. Untick the filter to browse the whole library.'
              : 'No video files found in the library.' }}
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Search } from 'lucide-vue-next'
import { apiUrl } from '../../config'
import { collectionLabel, folderOf, formatDate, formatSize } from '../../utils/format'
import type { TelegramCollection } from '../../api/cinegram'

type LocalFile = {
  path: string
  filename: string
  filesize: number
  modified_at: string
  version_tag: string | null
  quality: string | null
  tmdb_id: number | null
  tvdb_id: number | null
}

const props = defineProps<{
  collection: TelegramCollection
  tmdbId: number | null
  tvdbId: number | null
}>()

const emit = defineEmits<{
  close: []
  probed: []
}>()

const query = ref('')
const currentPath: string | null = props.collection.local_path ?? null
const onlyThisTitle = ref(Boolean(props.tmdbId || props.tvdbId))
const files = ref<LocalFile[]>([])
const loading = ref(false)
const submitting = ref(false)

const searchLocalFiles = async () => {
  loading.value = true
  try {
    const params = new URLSearchParams()
    const q = query.value.trim()
    if (q) params.set('q', q)
    if (onlyThisTitle.value) {
      if (props.tmdbId) params.set('tmdb_id', String(props.tmdbId))
      if (props.tvdbId) params.set('tvdb_id', String(props.tvdbId))
    }

    const res = await fetch(`${apiUrl}/local/files?${params.toString()}`)
    files.value = res.ok ? await res.json() : []
    if (!res.ok) alert('Error listing local files.')
  } catch (err) {
    console.error(err)
    alert('Connection error.')
  } finally {
    loading.value = false
  }
}

const probeLocalFile = async (path: string) => {
  submitting.value = true
  try {
    const res = await fetch(`${apiUrl}/probe/collection/${props.collection.id}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path })
    })
    if (res.ok) {
      emit('probed')
    } else {
      alert('❌ Error reading the file: ' + await res.text())
    }
  } catch (err) {
    console.error(err)
    alert('❌ Connection error.')
  } finally {
    submitting.value = false
  }
}

onMounted(searchLocalFiles)
</script>

<style scoped src="./buttons.css"></style>
<style scoped>
.probe-scope {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  font-size: 0.85rem;
  color: #a1a1aa;
  cursor: pointer;
}

.probe-results {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-height: 55vh;
  overflow-y: auto;
}

.probe-file {
  display: flex !important;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.4rem;
  width: 100%;
  padding: 0.75rem 1rem;
  text-align: left;
  background: rgba(255, 255, 255, 0.04);
  border-color: rgba(255, 255, 255, 0.08);
}

.probe-file-current {
  background: rgba(74, 222, 128, 0.08);
  border-color: rgba(74, 222, 128, 0.45);
}

.probe-file-name {
  font-weight: 600;
  color: #fff;
  line-height: 1.35;
  overflow-wrap: anywhere;
}

.probe-file-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.probe-badge {
  background: rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  padding: 0.15rem 0.45rem;
  font-size: 0.75rem;
  font-weight: 500;
  color: #d4d4d8;
  white-space: nowrap;
}

.probe-badge.quality {
  background: rgba(96, 165, 250, 0.18);
  color: #93c5fd;
  font-weight: 700;
}

.probe-badge.version {
  background: rgba(214, 186, 255, 0.16);
  color: #d6baff;
}

.probe-badge.current {
  background: rgba(74, 222, 128, 0.18);
  color: #4ade80;
  font-weight: 700;
}

.probe-file-path {
  font-size: 0.72rem;
  color: #71717a;
  line-height: 1.3;
  overflow-wrap: anywhere;
}
</style>
