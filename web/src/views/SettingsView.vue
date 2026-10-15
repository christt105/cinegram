<template>
  <div class="settings-container" style="max-width: 1000px; margin: 0 auto; padding: 2rem;">
    <h1>System Settings</h1>
    
    <div class="settings-grid" style="display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1.5rem; margin-top: 1.5rem;">
      <!-- Connection Status Card -->
      <div class="glass-panel settings-card" style="padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem;">
        <h3>Connection Status</h3>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Jellyfin URL:</span>
          <span class="value" style="font-family: monospace;">{{ jellyfinUrl ?? 'Not configured' }}</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Jellyfin Auth:</span>
          <span class="value success-text" v-if="jellyfinItems && jellyfinItems.length > 0" style="color: #4ade80; font-weight: 600;">Authenticated</span>
          <span class="value error-text" v-else style="color: #f87171; font-weight: 600;">Disconnected / Missing Token</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">API URL:</span>
          <span class="value" style="font-family: monospace;">{{ apiUrl }}</span>
        </div>
      </div>
      
      <!-- Library Statistics Card -->
      <div class="glass-panel settings-card" style="padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem;">
        <h3>Library Statistics</h3>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Movies in Telegram:</span>
          <span class="value" style="font-weight: 600;">{{ telegramMovies ? telegramMovies.length : 0 }}</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Series in Telegram:</span>
          <span class="value" style="font-weight: 600;">{{ telegramSeries ? telegramSeries.length : 0 }}</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Total Jellyfin Items:</span>
          <span class="value" style="font-weight: 600;">{{ jellyfinItems ? jellyfinItems.length : 0 }}</span>
        </div>
      </div>


      <!-- About Card -->
      <div class="glass-panel settings-card" style="padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem;">
        <h3>About</h3>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Version:</span>
          <span class="value" style="font-family: monospace;">v{{ appVersion }}</span>
        </div>
        <p style="color: var(--text-secondary); font-size: 0.95rem; margin: 0;">
          Cinegram is open source. Report issues or contribute on GitHub.
        </p>
        <a
          class="glass-button"
          href="https://github.com/christt105/cinegram"
          target="_blank"
          rel="noopener noreferrer"
          style="display: inline-flex; align-items: center; justify-content: center; gap: 0.5rem; padding: 0.6rem 1rem; text-decoration: none; align-self: flex-start;"
        >
          <Github :size="18" /> View on GitHub
        </a>
      </div>

      <!-- Maintenance and Anomalies Card -->
      <div class="glass-panel settings-card" style="grid-column: 1 / -1; margin-top: 1.5rem; padding: 1.5rem;">
        <h3 style="margin-bottom: 0.5rem;">Database & Integrity Maintenance</h3>
        <p style="color: var(--text-secondary); margin-bottom: 1.5rem; font-size: 0.95rem;">
          Collections or files that could not be correctly matched to any TMDB movie or series are shown here (orphaned records/anomalies).
        </p>

        <div v-if="loadingOrphans" class="loading-text" style="color: var(--text-secondary);">Loading anomalies...</div>
        <div v-else-if="orphans.length === 0" class="success-text" style="display: flex; align-items: center; gap: 0.5rem; color: #4ade80; font-weight: 600;">
          <span>✓</span> No anomalies or orphaned collections found in the database!
        </div>
        <div v-else class="orphans-section" style="display: flex; flex-direction: column; gap: 1rem;">
          <!-- Batch Actions Toolbar -->
          <div class="batch-toolbar glass-panel" style="padding: 0.75rem 1.25rem; display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; background: rgba(255, 255, 255, 0.03); border-radius: 12px; border-color: rgba(255,255,255,0.08);">
            <div style="display: flex; align-items: center; gap: 0.75rem;">
              <input type="checkbox" :checked="isAllSelected" @change="toggleSelectAll" style="width: 18px; height: 18px; cursor: pointer; accent-color: var(--jellyfin-blue);" />
              <span style="font-size: 0.95rem; font-weight: 500;">
                Selected: <strong style="color: var(--jellyfin-blue);">{{ selectedOrphanIds.length }}</strong> of <strong>{{ orphans.length }}</strong>
              </span>
            </div>
            
            <div style="display: flex; gap: 0.75rem;">
              <button @click="identifyBatch" :disabled="selectedOrphanIds.length === 0" class="glass-button" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;" :style="{ opacity: selectedOrphanIds.length === 0 ? 0.5 : 1 }">
                🔍 Batch Identify
              </button>
              <button @click="deleteBatch" :disabled="selectedOrphanIds.length === 0" class="glass-button danger" style="background: rgba(255, 180, 171, 0.12); border-color: rgba(255, 180, 171, 0.30); color: #ffb4ab; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;" :style="{ opacity: selectedOrphanIds.length === 0 ? 0.5 : 1 }">
                🗑️ Batch Delete
              </button>
            </div>
          </div>

          <!-- Orphans List -->
          <div class="orphans-list" style="display: flex; flex-direction: column; gap: 0.75rem;">
            <div v-for="orphan in orphans" :key="orphan.id" class="orphan-item glass-panel">
              <div class="orphan-header">
                <div class="orphan-checkbox-wrapper">
                  <input type="checkbox" :value="orphan.id" v-model="selectedOrphanIds" style="width: 18px; height: 18px; cursor: pointer; accent-color: var(--jellyfin-blue);" />
                </div>
                <div class="orphan-details">
                  <strong style="color: #ffb4ab; font-size: 1.05rem;">{{ orphan.name }}</strong>
                  <span style="font-size: 0.85rem; color: #a1a1aa;">
                    Collection ID: {{ orphan.id }} | Quality: {{ orphan.quality || 'Auto' }} | {{ orphan.files_count }} files
                  </span>
                  <ul style="margin: 0.5rem 0 0 1rem; padding: 0; font-size: 0.85rem; color: #888;">
                    <li v-for="file in orphan.files" :key="file.id" style="list-style-type: square; margin-bottom: 0.25rem;">
                      {{ file.filename }} <span style="color: #555;">({{ (file.filesize / (1024 * 1024)).toFixed(1) }} MB)</span>
                    </li>
                  </ul>
                </div>
              </div>
              
              <div class="orphan-actions">
                <button @click="openIdentifyModal(orphan)" class="glass-button" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;">
                  🔍 Identify
                </button>
                <button @click="deleteOrphan(orphan.id)" class="glass-button danger" style="background: rgba(255, 180, 171, 0.12); border-color: rgba(255, 180, 171, 0.30); color: #ffb4ab; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;">
                  🗑️ Delete
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- TMDB Search Modal -->
    <BaseModal v-if="searchModalOpen" title="Search in TMDB" @close="searchModalOpen = false">
      <div style="color: #a1a1aa; font-size: 0.85rem; word-break: break-all;">
        Identifying: <strong style="color: #ffb4ab;">{{ activeOrphanName }}</strong>
      </div>

      <TmdbSearchPanel media-type="multi" :initial-query="searchQuery" search-on-mount :select-disabled="identifying" @select="selectTMDBResult" />

      <template #footer>
        <button @click="searchModalOpen = false" class="glass-button">Close</button>
      </template>
    </BaseModal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { Github } from 'lucide-vue-next';
import BaseModal from '../components/BaseModal.vue';
import TmdbSearchPanel from '../components/tmdb/TmdbSearchPanel.vue';
import type { TmdbSearchResult } from '../composables/useTmdbSearch';

const props = defineProps<{
  jellyfinItems: any[];
  telegramMovies: any[];
  telegramSeries: any[];
}>();

import { apiUrl, jellyfinProxyUrl, appVersion } from '../config';

const jellyfinUrl = ref<string | null>(null);

const fetchJellyfinUrl = async () => {
  try {
    const res = await fetch(jellyfinProxyUrl);
    if (res.ok) jellyfinUrl.value = (await res.json()).url;
  } catch (err) {
    console.error(err);
  }
};

const orphans = ref<any[]>([]);
const loadingOrphans = ref(true);
const selectedOrphanIds = ref<number[]>([]);

const isAllSelected = computed(() => {
  return orphans.value.length > 0 && selectedOrphanIds.value.length === orphans.value.length;
});

const toggleSelectAll = (e: any) => {
  if (e.target.checked) {
    selectedOrphanIds.value = orphans.value.map(o => o.id);
  } else {
    selectedOrphanIds.value = [];
  }
};

const fetchOrphans = async () => {
  loadingOrphans.value = true;
  selectedOrphanIds.value = [];
  try {
    const res = await fetch(`${apiUrl}/maintenance/orphans`);
    if (res.ok) {
      orphans.value = await res.json();
    }
  } catch (err) {
    console.error(err);
  } finally {
    loadingOrphans.value = false;
  }
};

const searchModalOpen = ref(false);
const searchQuery = ref("");
const identifying = ref(false);
const activeOrphanId = ref<number | null>(null);
const activeOrphanName = ref("");

const openIdentifyModal = (orphan: any) => {
  activeOrphanId.value = orphan.id;
  activeOrphanName.value = orphan.name;
  
  // Clean up name for search query (remove tags like [1080p], .zip, etc.)
  let cleanQuery = orphan.name
    .replace(/\[.*?\]/g, "")
    .replace(/\(.*?\)/g, "")
    .replace(/\.(zip|7z|rar|mkv|mp4|avi)$/i, "")
    .replace(/[-_]+/g, " ")
    .trim();
    
  searchQuery.value = cleanQuery;
  searchModalOpen.value = true;
};

const selectTMDBResult = async (result: TmdbSearchResult) => {
  if (!activeOrphanId.value) return;
  identifying.value = true;
  try {
    const res = await fetch(`${apiUrl}/collections/${activeOrphanId.value}/identify`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ tmdb_id: result.id, media_type: result.media_type })
    });
    if (res.ok) {
      alert(`Collection successfully linked to "${result.title}" (${result.year})`);
      searchModalOpen.value = false;
      fetchOrphans();
    } else {
      const txt = await res.text();
      alert("Error identifying: " + txt);
    }
  } catch (err) {
    console.error(err);
    alert("Connection error.");
  } finally {
    identifying.value = false;
  }
};

const identifyBatch = async () => {
  if (selectedOrphanIds.value.length === 0) return;
  const tmdbIdStr = prompt(`Enter the TMDB ID to identify the ${selectedOrphanIds.value.length} selected collections at once:`);
  if (!tmdbIdStr) return;
  const tmdbId = intVal(tmdbIdStr.trim());
  if (isNaN(tmdbId)) {
    alert("The TMDB ID must be a valid number.");
    return;
  }
  
  try {
    const res = await fetch(`${apiUrl}/maintenance/identify-batch`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        collection_ids: selectedOrphanIds.value,
        tmdb_id: tmdbId
      })
    });
    if (res.ok) {
      alert("Batch identification completed successfully.");
      fetchOrphans();
    } else {
      alert("Error processing batch.");
    }
  } catch (err) {
    console.error(err);
    alert("Connection error.");
  }
};

const deleteBatch = async () => {
  if (selectedOrphanIds.value.length === 0) return;
  if (!confirm(`Are you sure you want to delete the ${selectedOrphanIds.value.length} selected collections at once? This action is permanent.`)) return;
  
  try {
    const res = await fetch(`${apiUrl}/maintenance/delete-batch`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        collection_ids: selectedOrphanIds.value
      })
    });
    if (res.ok) {
      alert("Batch deletion completed.");
      fetchOrphans();
    } else {
      alert("Error deleting batch.");
    }
  } catch (err) {
    console.error(err);
    alert("Connection error.");
  }
};

const intVal = (val: string) => {
  return parseInt(val);
};

const deleteOrphan = async (colId: number) => {
  if (!confirm("Are you sure you want to delete this orphaned collection from the database? Its file references will also be removed.")) return;
  try {
    const res = await fetch(`${apiUrl}/collections/${colId}`, {
      method: "DELETE"
    });
    if (res.ok) {
      alert("Orphaned collection deleted.");
      fetchOrphans();
    } else {
      alert("Error deleting.");
    }
  } catch (err) {
    console.error(err);
  }
};

onMounted(() => {
  fetchOrphans();
  fetchJellyfinUrl();
});
</script>

<style scoped>
.orphan-item {
  padding: 1rem;
  background: rgba(0, 0, 0, 0.2);
  display: flex;
  flex-direction: column;
  gap: 1rem;
  border-radius: 12px;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.orphan-header {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  width: 100%;
}

.orphan-checkbox-wrapper {
  padding-top: 0.25rem;
}

.orphan-details {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex-grow: 1;
}

.orphan-actions {
  display: flex;
  gap: 0.5rem;
  width: 100%;
  justify-content: flex-end;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
  padding-top: 0.75rem;
}

.orphan-actions button {
  flex: 1;
  max-width: 150px;
  text-align: center;
  justify-content: center;
}

@media (min-width: 640px) {
  .orphan-item {
    flex-direction: row;
    align-items: center;
    gap: 1.25rem;
  }
  
  .orphan-actions {
    width: auto;
    border-top: none;
    padding-top: 0;
    justify-content: flex-start;
  }
  
  .orphan-actions button {
    flex: none;
  }
}
</style>

