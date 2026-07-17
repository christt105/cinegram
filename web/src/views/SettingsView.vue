<template>
  <div class="settings-container" style="max-width: 1000px; margin: 0 auto; padding: 2rem;">
    <h1>System Settings</h1>
    
    <div class="settings-grid" style="display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1.5rem; margin-top: 1.5rem;">
      <!-- Connection Status Card -->
      <div class="glass-panel settings-card" style="padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem;">
        <h3>Connection Status</h3>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Jellyfin URL:</span>
          <span class="value" style="font-family: monospace;">{{ jellyfinUrl }}</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Jellyfin Auth:</span>
          <span class="value success-text" v-if="jellyfinItems && jellyfinItems.length > 0" style="color: #4ade80; font-weight: 600;">Authenticated</span>
          <span class="value error-text" v-else style="color: #f87171; font-weight: 600;">Disconnected / Missing Token</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Backend API URL:</span>
          <span class="value" style="font-family: monospace;">{{ backendUrl }}</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Telegram Bot Username:</span>
          <span class="value" style="color: #60a5fa; font-weight: 500;">@BibliotecaKachopinesBot</span>
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

      <!-- Telegram Link Configuration Card -->
      <div class="glass-panel settings-card" style="padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem;">
        <h3>Telegram Message Links</h3>
        <p style="color: var(--text-secondary); font-size: 0.85rem; margin: 0;">
          Configure the link prefix to navigate directly to file messages in Telegram from the library.
        </p>
        <div>
          <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Telegram Link Base:</label>
          <input v-model="telegramLinkBase" @change="saveTelegramLinkBase" type="text" placeholder="https://t.me/YourBotName or https://t.me/c/1234567890" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-family: monospace; font-size: 0.9rem;" />
        </div>
        <div style="font-size: 0.75rem; color: #6b7280; display: flex; flex-direction: column; gap: 0.25rem;">
          <span>• <strong>Private Group/Channel:</strong> Use <code>https://t.me/c/CHAT_ID</code> (without the -100 prefix).</span>
          <span>• <strong>Bot Chat:</strong> Use <code>https://t.me/YourBotName</code>.</span>
        </div>
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
              <button @click="identifyBatch" :disabled="selectedOrphanIds.length === 0" class="glass-button" style="background: rgba(59, 130, 246, 0.15); border-color: rgba(59, 130, 246, 0.35); color: #93c5fd; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;" :style="{ opacity: selectedOrphanIds.length === 0 ? 0.5 : 1 }">
                🔍 Batch Identify
              </button>
              <button @click="deleteBatch" :disabled="selectedOrphanIds.length === 0" class="glass-button danger" style="background: rgba(239, 68, 68, 0.15); border-color: rgba(239, 68, 68, 0.35); color: #fca5a5; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;" :style="{ opacity: selectedOrphanIds.length === 0 ? 0.5 : 1 }">
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
                  <strong style="color: #fca5a5; font-size: 1.05rem;">{{ orphan.name }}</strong>
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
                <button @click="openIdentifyModal(orphan)" class="glass-button" style="background: rgba(59, 130, 246, 0.15); border-color: rgba(59, 130, 246, 0.35); color: #93c5fd; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;">
                  🔍 Identify
                </button>
                <button @click="deleteOrphan(orphan.id)" class="glass-button danger" style="background: rgba(239, 68, 68, 0.15); border-color: rgba(239, 68, 68, 0.35); color: #fca5a5; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;">
                  🗑️ Delete
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- TMDB Search Modal -->
    <div v-if="searchModalOpen" class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px); padding: 1rem;">
      <div class="glass-panel" style="width: 100%; max-width: 600px; max-height: 85vh; display: flex; flex-direction: column; gap: 1rem; padding: 1.5rem; background: rgba(15, 23, 42, 0.95); border: 1px solid rgba(255,255,255,0.08); border-radius: 16px; overflow: hidden; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5);">
        
        <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">
          <h3 style="margin: 0; font-size: 1.2rem; color: #fff;">Search in TMDB</h3>
          <button @click="searchModalOpen = false" class="glass-button icon-only" style="padding: 0; border-radius: 50%; width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-size: 14px;">✕</button>
        </div>
        
        <div style="color: #a1a1aa; font-size: 0.85rem; word-break: break-all;">
          Identifying: <strong style="color: #fca5a5;">{{ activeOrphanName }}</strong>
        </div>

        <!-- Search Bar -->
        <div style="display: flex; gap: 0.5rem; width: 100%;">
          <input v-model="searchQuery" @keyup.enter="searchTMDB" type="text" placeholder="Type the series or movie name..." style="flex-grow: 1; padding: 10px 14px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-size: 0.95rem;" />
          <button @click="searchTMDB" class="glass-button primary" style="padding: 0 1.25rem;">Search</button>
        </div>

        <!-- Results List -->
        <div style="flex-grow: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 0.75rem; padding-right: 0.25rem;">
          <div v-if="isSearching" style="text-align: center; padding: 2rem; color: #a1a1aa;">
            <div style="margin-bottom: 0.5rem;">Searching...</div>
          </div>
          <div v-else-if="searchResults.length === 0 && searchQuery" style="text-align: center; padding: 2rem; color: #a1a1aa;">
            No results found.
          </div>
          
          <div v-for="result in searchResults" :key="result.id" class="result-card" style="display: flex; gap: 1rem; padding: 0.75rem; background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.05); border-radius: 10px; transition: background 0.2s;">
            <img v-if="result.poster_path" :src="'https://image.tmdb.org/t/p/w92' + result.poster_path" style="width: 50px; height: 75px; object-fit: cover; border-radius: 6px; flex-shrink: 0;" />
            <div v-else style="width: 50px; height: 75px; background: rgba(255,255,255,0.05); border-radius: 6px; display: flex; align-items: center; justify-content: center; font-size: 0.75rem; color: #555; text-align: center; flex-shrink: 0;">No Poster</div>
            
            <div style="display: flex; flex-direction: column; gap: 0.25rem; flex-grow: 1; min-width: 0;">
              <div style="display: flex; flex-wrap: wrap; align-items: baseline; gap: 0.5rem;">
                <strong style="color: #fff; font-size: 0.95rem;">{{ result.title }}</strong>
                <span style="color: #6b7280; font-size: 0.8rem;">({{ result.year }})</span>
              </div>
              <span class="badge" :style="{
                background: result.media_type === 'movie' ? 'rgba(59, 130, 246, 0.15)' : 'rgba(16, 185, 129, 0.15)',
                color: result.media_type === 'movie' ? '#93c5fd' : '#a7f3d0',
                fontSize: '0.7rem',
                padding: '2px 6px',
                borderRadius: '4px',
                width: 'fit-content',
                textTransform: 'uppercase',
                fontWeight: '600'
              }">{{ result.media_type === 'movie' ? 'Movie' : 'Series' }}</span>
              <p style="margin: 0.25rem 0 0 0; font-size: 0.75rem; color: #a1a1aa; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; text-overflow: ellipsis; line-height: 1.3;">{{ result.overview || 'No description available.' }}</p>
            </div>
            
            <button @click="selectTMDBResult(result)" class="glass-button" style="align-self: center; background: rgba(59, 130, 246, 0.15); border-color: rgba(59, 130, 246, 0.35); color: #93c5fd; padding: 6px 12px; font-size: 0.8rem; border-radius: 6px; flex-shrink: 0;">
              Select
            </button>
          </div>
        </div>
        
        <div style="display: flex; justify-content: flex-end; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
          <button @click="searchModalOpen = false" class="glass-button" style="padding: 6px 16px;">Close</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';

const props = defineProps<{
  jellyfinItems: any[];
  telegramMovies: any[];
  telegramSeries: any[];
}>();

const jellyfinUrl = import.meta.env.VITE_JELLYFIN_URL || 'http://192.168.1.15:8096';
const backendUrl = import.meta.env.VITE_BACKEND_URL || `${window.location.protocol}//${window.location.hostname}:8005`;

const telegramLinkBase = ref(localStorage.getItem('telegram_link_base') || 'https://t.me/BibliotecaKachopinesBot');
const saveTelegramLinkBase = () => {
  localStorage.setItem('telegram_link_base', telegramLinkBase.value.trim());
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
    const res = await fetch(`${backendUrl}/maintenance/orphans`);
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
const searchResults = ref<any[]>([]);
const isSearching = ref(false);
const activeOrphanId = ref<number | null>(null);
const activeOrphanName = ref("");

const searchTMDB = async () => {
  if (!searchQuery.value.trim()) return;
  isSearching.value = true;
  searchResults.value = [];
  try {
    const res = await fetch(`${backendUrl}/tmdb/search?query=${encodeURIComponent(searchQuery.value.trim())}`);
    if (res.ok) {
      searchResults.value = await res.json();
    }
  } catch (err) {
    console.error(err);
  } finally {
    isSearching.value = false;
  }
};

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
  searchTMDB(); // trigger auto search
};

const selectTMDBResult = async (result: any) => {
  if (!activeOrphanId.value) return;
  try {
    const res = await fetch(`${backendUrl}/collections/${activeOrphanId.value}/identify`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ tmdb_id: result.id })
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
  }
};

const identifyOrphan = async (colId: number) => {
  const tmdbIdStr = prompt("Enter the TMDB ID to identify this collection:");
  if (!tmdbIdStr) return;
  const tmdbId = intVal(tmdbIdStr.trim());
  if (isNaN(tmdbId)) {
    alert("The TMDB ID must be a valid number.");
    return;
  }
  
  try {
    const res = await fetch(`${backendUrl}/collections/${colId}/identify`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ tmdb_id: tmdbId })
    });
    if (res.ok) {
      alert("Identified and linked successfully.");
      fetchOrphans();
    } else {
      const txt = await res.text();
      alert("Error identifying: " + txt);
    }
  } catch (err) {
    console.error(err);
    alert("Connection error.");
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
    const res = await fetch(`${backendUrl}/maintenance/identify-batch`, {
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
    const res = await fetch(`${backendUrl}/maintenance/delete-batch`, {
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
    const res = await fetch(`${backendUrl}/collections/${colId}`, {
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

