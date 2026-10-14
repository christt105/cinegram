<template>
  <div class="downloads-view">
    <div class="content-header">
      <div class="content-heading">
        <h1>Downloads</h1>
        <p class="content-subtitle">Files detected in the downloads folder, guessed against TMDB</p>
      </div>
      <div class="content-header-actions">
        <button
          v-if="view === 'pending'"
          class="glass-button"
          :disabled="isReidentifying"
          title="Re-run TMDB identification for every pending file"
          @click="reidentifyAll"
        >
          <Sparkles :size="16" :class="{ spinning: isReidentifying }" />
          Retry identification
        </button>
        <button
          class="glass-button"
          :disabled="isRescanning"
          title="Look for files the folder watcher missed"
          @click="rescanFolder"
        >
          <FolderSearch :size="16" :class="{ spinning: isRescanning }" />
          Scan folder
        </button>
        <button class="glass-button primary" @click="fetchFiles">
          <RefreshCw :size="16" :class="{ spinning: isLoading }" />
          Refresh
        </button>
      </div>
    </div>

    <div class="tabs">
      <button class="glass-button" :class="{ active: view === 'pending' }" @click="view = 'pending'">
        Pending
        <span v-if="pendingRows.length" class="tab-count">{{ pendingRows.length }}</span>
      </button>
      <button class="glass-button" :class="{ active: view === 'history' }" @click="view = 'history'">
        History
      </button>
    </div>

    <div v-if="view === 'pending'" class="glass-panel batch-bar">
      <div class="batch-bar-selection">
        <button class="glass-button" @click="selectAllPending">Select all pending</button>
        <button class="glass-button" :disabled="selectedIds.size === 0" @click="clearSelection">Clear selection</button>
        <span class="selection-count">{{ selectedIds.size }} selected</span>
      </div>
      <div class="batch-bar-actions">
        <button
          class="glass-button success"
          :disabled="selectedIds.size === 0 || isBatchActing"
          @click="confirmSelected"
        >
          <Check :size="16" />
          Confirm selected
        </button>
        <button
          class="glass-button"
          :disabled="selectedIds.size === 0 || isBatchActing"
          @click="openBatchCorrect"
        >
          <Edit3 :size="16" />
          Correct selected
        </button>
      </div>
    </div>

    <div v-if="isLoading && files.length === 0" class="empty-state">Loading&hellip;</div>
    <div v-else-if="visibleRows.length === 0" class="empty-state">
      {{ view === 'pending' ? 'Nothing pending — the downloads folder is all caught up.' : 'No history yet.' }}
    </div>
    <div v-else class="task-list">
      <WatchedFileCard
        v-for="row in visibleRows"
        :key="row.id"
        :row="row"
        :pending="view === 'pending'"
        :selected="selectedIds.has(row.id)"
        :busy="isBatchActing"
        @toggle-select="toggleSelect(row.id)"
        @confirm="confirmRow(row)"
        @correct="openSingleCorrect(row)"
      />
    </div>

    <CorrectIdentityModal
      v-if="correcting"
      :target-row="correctTarget"
      :selected-count="selectedIds.size"
      @close="correcting = false"
      @corrected="correcting = false; fetchFiles()"
      @batch-select="applyBatchCorrection"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { RefreshCw, Check, Edit3, Sparkles, FolderSearch } from 'lucide-vue-next';
import { confirmWatchedFile, correctWatchedFile, type WatchedFile } from '../api/cinegram';
import { useWatchedFiles, guessLabel } from '../composables/useWatchedFiles';
import type { TmdbSearchResult } from '../composables/useTmdbSearch';
import WatchedFileCard from '../components/downloads/WatchedFileCard.vue';
import CorrectIdentityModal from '../components/downloads/CorrectIdentityModal.vue';

const {
  files,
  isLoading,
  isReidentifying,
  isRescanning,
  pendingRows,
  historyRows,
  fetchFiles,
  reidentifyAll,
  rescanFolder
} = useWatchedFiles();

const view = ref<'pending' | 'history'>('pending');
const selectedIds = ref<Set<number>>(new Set());
const isBatchActing = ref(false);

const visibleRows = computed(() => (view.value === 'pending' ? pendingRows.value : historyRows.value));

const toggleSelect = (id: number) => {
  if (selectedIds.value.has(id)) {
    selectedIds.value.delete(id);
  } else {
    selectedIds.value.add(id);
  }
  selectedIds.value = new Set(selectedIds.value);
};

const selectAllPending = () => {
  const pending = files.value.filter(f => f.status === 'pending' || f.status === 'notified');
  selectedIds.value = new Set(pending.map(f => f.id));
};

const clearSelection = () => {
  selectedIds.value = new Set();
};

const confirmRow = async (row: WatchedFile) => {
  if (!row.guess_tmdb_id) return;
  if (!confirm(`Move "${row.filename}" into the library as "${guessLabel(row)}"?`)) return;
  try {
    await confirmWatchedFile(row.id, row.guess_tmdb_id, row.guess_season, row.guess_episode);
    await fetchFiles();
  } catch (err) {
    console.error(err);
    alert(`Failed to confirm ${row.filename}.`);
  }
};

const confirmSelected = async () => {
  const count = selectedIds.value.size;
  if (!confirm(`Move ${count} selected file(s) into the library?`)) return;

  isBatchActing.value = true;
  const targets = files.value.filter(f => selectedIds.value.has(f.id));
  const skipped: string[] = [];
  const failed: string[] = [];

  try {
    for (const row of targets) {
      if (!row.guess_tmdb_id) {
        skipped.push(row.filename);
        continue;
      }
      try {
        await confirmWatchedFile(row.id, row.guess_tmdb_id, row.guess_season, row.guess_episode);
      } catch (err) {
        console.error(err);
        failed.push(row.filename);
      }
    }
  } finally {
    isBatchActing.value = false;
    clearSelection();
    await fetchFiles();
  }

  if (skipped.length || failed.length) {
    const parts: string[] = [];
    if (skipped.length) parts.push(`No TMDB guess, skipped: ${skipped.join(', ')}`);
    if (failed.length) parts.push(`Failed to confirm: ${failed.join(', ')}`);
    alert(parts.join('\n'));
  }
};

const correcting = ref(false);
const correctTarget = ref<WatchedFile | null>(null);

const openSingleCorrect = (row: WatchedFile) => {
  correctTarget.value = row;
  correcting.value = true;
};

const openBatchCorrect = () => {
  correctTarget.value = null;
  correcting.value = true;
};

const applyBatchCorrection = async (result: TmdbSearchResult) => {
  isBatchActing.value = true;
  correcting.value = false;
  const targets = files.value.filter(f => selectedIds.value.has(f.id));
  const failed: string[] = [];

  try {
    for (const row of targets) {
      try {
        await correctWatchedFile(row.id, result.id, row.guess_season, row.guess_episode);
      } catch (err) {
        console.error(err);
        failed.push(row.filename);
      }
    }
  } finally {
    isBatchActing.value = false;
    clearSelection();
    await fetchFiles();
  }

  if (failed.length) {
    alert(`Failed to correct: ${failed.join(', ')}`);
  }
};
</script>

<style scoped src="../components/downloads/buttons.css"></style>
<style scoped>
.downloads-view {
  display: flex;
  flex-direction: column;
}

.content-header-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.tabs {
  display: flex;
  gap: 8px;
  margin-bottom: var(--sp-sm);
}

.tab-count {
  margin-left: 6px;
  opacity: 0.7;
  font-size: 0.8em;
}

.batch-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
  padding: 14px 18px;
  margin-bottom: var(--sp-md);
}

.batch-bar-selection,
.batch-bar-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.selection-count {
  font-size: 0.85rem;
  color: var(--on-surface-variant);
}

.task-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding-bottom: 24px;
}

.empty-state {
  padding: 32px;
  text-align: center;
  color: var(--text-secondary);
  background: rgba(0, 0, 0, 0.2);
  border-radius: 12px;
  border: 1px dashed var(--glass-border);
}

.spinning {
  animation: spin 1s linear infinite;
}
</style>
