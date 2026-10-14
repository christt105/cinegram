<template>
  <div class="glass-panel task-card" :class="{ dimmed: row.status === 'removed' }">
    <div class="task-info">
      <div class="task-selectblock">
        <input
          v-if="pending"
          type="checkbox"
          :checked="selected"
          @change="emit('toggle-select')"
        />
        <div class="task-titleblock">
          <h4>{{ row.filename }}</h4>
          <span class="task-direction label-caps">
            {{ guessLabel(row) }}
            <a
              v-if="tmdbUrl"
              :href="tmdbUrl"
              target="_blank"
              rel="noopener"
              class="tmdb-link"
              title="Open on TMDB"
              @click.stop
            >
              <ExternalLink :size="12" />
              TMDB
            </a>
          </span>
        </div>
      </div>
      <div class="task-badges">
        <span class="badge confidence" :class="confidenceClass">
          {{ Math.round(row.confidence * 100) }}%
        </span>
        <span class="badge" :class="row.status">{{ row.status }}</span>
      </div>
    </div>

    <div v-if="pending" class="task-row-actions">
      <button
        class="glass-button success btn-sm"
        :disabled="!row.guess_tmdb_id || busy"
        title="Confirm using this row's current guess"
        @click="emit('confirm')"
      >
        <Check :size="14" />
        Confirm
      </button>
      <button class="glass-button btn-sm" :disabled="busy" @click="emit('correct')">
        <Edit3 :size="14" />
        Correct
      </button>
    </div>

    <div class="task-meta">
      <span>{{ row.path }}</span>
      <span>{{ formatBytes(row.filesize) }}</span>
    </div>

    <div v-if="row.status === 'removed'" class="removed-msg">
      File was removed from the downloads folder before it was actioned.
    </div>
    <div v-if="row.status === 'moved' && row.moved_path" class="moved-msg">
      Moved to {{ row.moved_path }}
    </div>
    <div v-if="row.status === 'error' && row.error_message" class="error-msg">
      {{ row.error_message }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { Check, Edit3, ExternalLink } from 'lucide-vue-next'
import type { WatchedFile } from '../../api/cinegram'
import { guessLabel } from '../../composables/useWatchedFiles'
import { formatBytes } from '../../utils/format'

const props = defineProps<{
  row: WatchedFile
  pending: boolean
  selected: boolean
  busy: boolean
}>()

const emit = defineEmits<{
  'toggle-select': []
  confirm: []
  correct: []
}>()

// A close-looking wrong guess (e.g. an obscure same-named title, or a series shown under a
// localized name) is hard to catch from the guessed title alone, so link straight to TMDB
// to sanity-check it before confirming.
const tmdbUrl = computed(() => {
  const row = props.row
  if (!row.guess_tmdb_id || (row.guess_media_type !== 'movie' && row.guess_media_type !== 'tv')) return null
  return `https://www.themoviedb.org/${row.guess_media_type}/${row.guess_tmdb_id}`
})

const confidenceClass = computed(() => {
  if (props.row.confidence >= 0.8) return 'high'
  if (props.row.confidence >= 0.5) return 'medium'
  return 'low'
})
</script>

<style scoped src="./buttons.css"></style>
<style scoped>
.task-card {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.task-card.dimmed {
  opacity: 0.5;
}

.task-info {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
}

.task-selectblock {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  min-width: 0;
}

.task-selectblock input[type="checkbox"] {
  margin-top: 4px;
  width: 16px;
  height: 16px;
  flex-shrink: 0;
  accent-color: var(--primary);
}

.task-titleblock {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.task-titleblock h4 {
  margin: 0;
  font-size: 1rem;
  color: var(--text-primary);
  word-break: break-all;
}

.task-direction {
  color: var(--on-surface-variant);
  opacity: 0.7;
  font-size: 11px;
}

.tmdb-link {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  margin-left: 6px;
  color: var(--primary);
  opacity: 1;
  text-decoration: none;
}

.tmdb-link:hover {
  text-decoration: underline;
}

.task-badges {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}

.task-row-actions {
  display: flex;
  gap: 8px;
}

.task-meta {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.task-meta span:first-child {
  word-break: break-all;
}

.badge {
  padding: 4px 10px;
  border-radius: var(--r-full);
  font-family: 'Geist', 'Inter', sans-serif;
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  white-space: nowrap;
}

.badge.pending { background: rgba(245, 191, 0, 0.15); color: var(--warning); border: 1px solid rgba(245, 191, 0, 0.25); }
.badge.notified { background: rgba(214, 186, 255, 0.15); color: var(--secondary); border: 1px solid rgba(214, 186, 255, 0.25); }
.badge.confirmed, .badge.corrected { background: rgba(34, 197, 94, 0.12); color: var(--success); border: 1px solid rgba(34, 197, 94, 0.25); }
.badge.moved { background: rgba(34, 197, 94, 0.2); color: var(--success); border: 1px solid rgba(34, 197, 94, 0.35); }
.badge.removed { background: rgba(255, 255, 255, 0.06); color: var(--on-surface-variant); border: 1px solid var(--glass-border); }
.badge.error { background: rgba(255, 180, 171, 0.12); color: var(--error); border: 1px solid rgba(255, 180, 171, 0.25); }

.badge.confidence.high { background: rgba(34, 197, 94, 0.12); color: var(--success); border: 1px solid rgba(34, 197, 94, 0.25); }
.badge.confidence.medium { background: rgba(245, 191, 0, 0.15); color: var(--warning); border: 1px solid rgba(245, 191, 0, 0.25); }
.badge.confidence.low { background: rgba(255, 180, 171, 0.12); color: var(--error); border: 1px solid rgba(255, 180, 171, 0.25); }

.removed-msg {
  font-size: 0.8rem;
  color: var(--on-surface-variant);
  background: rgba(255, 255, 255, 0.03);
  padding: 8px;
  border-radius: 4px;
}

.moved-msg {
  font-size: 0.8rem;
  color: var(--success);
  background: rgba(34, 197, 94, 0.08);
  padding: 8px;
  border-radius: 4px;
}

.error-msg {
  font-size: 0.8rem;
  color: var(--error);
  background: rgba(239, 68, 68, 0.1);
  padding: 8px;
  border-radius: 4px;
}

@media (max-width: 768px) {
  .task-info {
    flex-direction: column;
  }
}
</style>
