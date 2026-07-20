<template>
  <div class="queue-view">
    <div class="content-header">
      <div class="content-heading">
        <h1>Transfers</h1>
        <p class="content-subtitle">Media flow between the Jellyfin server and Telegram cloud storage</p>
      </div>
      <button class="glass-button primary" @click="fetchQueues">
        <RefreshCw :size="16" :class="{ spinning: isLoading }" />
        Refresh
      </button>
    </div>

    <div class="stats-bento">
      <div class="stat-card glass-panel">
        <span class="stat-label label-caps">Active Uploads</span>
        <span class="stat-value" style="color: var(--primary);">{{ uploads.length }}</span>
      </div>
      <div class="stat-card glass-panel">
        <span class="stat-label label-caps">Active Downloads</span>
        <span class="stat-value" style="color: var(--secondary);">{{ downloads.length }}</span>
      </div>
      <div class="stat-card glass-panel">
        <span class="stat-label label-caps">Failed</span>
        <span class="stat-value" style="color: var(--error);">{{ failedCount }}</span>
      </div>
      <div class="stat-card glass-panel">
        <span class="stat-label label-caps">System Status</span>
        <span class="stat-status">
          <span class="status-dot"></span>
          {{ isLoading ? 'Syncing' : 'Stable' }}
        </span>
      </div>
    </div>

    <div class="queue-container">
      <div class="queue-section">
        <h2>Uploads (To Telegram)</h2>
        <div v-if="uploads.length === 0" class="empty-state">
          No active uploads.
        </div>
        <div v-else class="task-list">
          <div v-for="task in uploads" :key="task.id" class="glass-panel task-card">
            <div class="task-info">
              <div class="task-titleblock">
                <h4>{{ task.title || 'Unknown Media' }} ({{ task.year || '' }})</h4>
                <span class="task-direction label-caps">Jellyfin &rarr; Telegram</span>
              </div>
              <div style="display:flex; gap:0.5rem; align-items:center;">
                <span class="badge" :class="task.status">{{ task.status }}</span>
                <button v-if="task.status === 'failed'" @click="retryUpload(task.id)" class="glass-button success btn-sm icon-only" title="Retry" style="background: rgba(16, 185, 129, 0.15); border-color: rgba(16, 185, 129, 0.35); color: #a7f3d0; display: inline-flex; align-items: center; justify-content: center; padding: 4px;">
                  <RefreshCw :size="14" />
                </button>
                <button @click="cancelUpload(task.id)" class="glass-button danger btn-sm icon-only" title="Cancel">
                  <X :size="14" />
                </button>
              </div>
            </div>
            <div class="progress-bar-container">
              <div class="progress-bar" :style="{ width: (task.progress || 0) + '%' }"></div>
            </div>
            <div class="task-meta">
              <span>{{ task.media_type }}</span>
              <span>{{ task.progress || 0 }}%</span>
            </div>
            <div class="error-msg" v-if="task.error_message">{{ task.error_message }}</div>
          </div>
        </div>
      </div>

      <div class="queue-section">
        <h2>Downloads (To Local Storage)</h2>
        <div v-if="downloads.length === 0" class="empty-state">
          No active downloads.
        </div>
        <div v-else class="task-list">
          <div v-for="task in downloads" :key="task.id" class="glass-panel task-card">
            <div class="task-info">
              <div class="task-titleblock">
                <h4>{{ task.title || 'Collection ID: ' + task.collection_id }}</h4>
                <span class="task-direction label-caps">Telegram &rarr; Jellyfin</span>
              </div>
              <div style="display:flex; gap:0.5rem; align-items:center;">
                <span class="badge" :class="task.status">{{ task.status }}</span>
                <button v-if="task.status === 'failed'" @click="retryDownload(task.id)" class="glass-button success btn-sm icon-only" title="Retry" style="background: rgba(16, 185, 129, 0.15); border-color: rgba(16, 185, 129, 0.35); color: #a7f3d0; display: inline-flex; align-items: center; justify-content: center; padding: 4px;">
                  <RefreshCw :size="14" />
                </button>
                <button @click="cancelDownload(task.id)" class="glass-button danger btn-sm icon-only" title="Cancel">
                  <X :size="14" />
                </button>
              </div>
            </div>
            <div class="progress-bar-container">
              <div class="progress-bar" :style="{ width: (task.progress || 0) + '%' }"></div>
            </div>
            <div class="task-meta">
              <span>{{ task.progress || 0 }}%</span>
            </div>
            <div class="error-msg" v-if="task.error_message">{{ task.error_message }}</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { RefreshCw, X } from 'lucide-vue-next';

interface UploadTask {
  id: number;
  jellyfin_id: string;
  title: string;
  year: number;
  media_type: string;
  status: string;
  progress: number;
  error_message?: string;
}

interface DownloadTask {
  id: number;
  collection_id: number;
  status: string;
  progress: number;
  error_message?: string;
}

const uploads = ref<UploadTask[]>([]);
const downloads = ref<DownloadTask[]>([]);
const isLoading = ref(false);
const failedCount = computed(() =>
  uploads.value.filter(t => t.status === 'failed').length +
  downloads.value.filter(t => t.status === 'failed').length
);
let pollInterval: any = null;
const backendUrl = import.meta.env.VITE_BACKEND_URL || `${window.location.protocol}//${window.location.hostname}:8005`;

const fetchQueues = async () => {
  isLoading.value = true;
  try {
    const [upRes, downRes] = await Promise.all([
      fetch(`${backendUrl}/uploads/queue`),
      fetch(`${backendUrl}/downloads/queue`)
    ]);
    
    if (upRes.ok) uploads.value = await upRes.json();
    if (downRes.ok) downloads.value = await downRes.json();
  } catch (error) {
    console.error('Error fetching queues:', error);
  } finally {
    isLoading.value = false;
  }
};

const cancelUpload = async (id: number) => {
  if (!confirm("Cancel this upload task?")) return;
  try {
    await fetch(`${backendUrl}/uploads/${id}`, { method: 'DELETE' });
    fetchQueues();
  } catch (err) {
    console.error(err);
  }
};

const retryUpload = async (id: number) => {
  try {
    const res = await fetch(`${backendUrl}/uploads/${id}/retry`, { method: 'POST' });
    if (res.ok) {
      fetchQueues();
    }
  } catch (err) {
    console.error(err);
  }
};

const cancelDownload = async (id: number) => {
  if (!confirm("Cancel this download task?")) return;
  try {
    await fetch(`${backendUrl}/downloads/${id}`, { method: 'DELETE' });
    fetchQueues();
  } catch (err) {
    console.error(err);
  }
};

const retryDownload = async (id: number) => {
  try {
    const res = await fetch(`${backendUrl}/downloads/${id}/retry`, { method: 'POST' });
    if (res.ok) {
      fetchQueues();
    }
  } catch (err) {
    console.error(err);
  }
};

onMounted(() => {
  fetchQueues();
  pollInterval = setInterval(fetchQueues, 5000);
});

onUnmounted(() => {
  if (pollInterval) clearInterval(pollInterval);
});
</script>

<style scoped>
.queue-view {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.stats-bento {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: var(--gutter);
  margin-bottom: var(--sp-lg);
}

.stat-card {
  padding: var(--sp-md);
  border-radius: var(--r-xl);
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.stat-label {
  color: var(--on-surface-variant);
  opacity: 0.7;
}

.stat-value {
  font-size: 2.25rem;
  font-weight: 800;
  line-height: 1;
}

.stat-status {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 1.15rem;
  font-weight: 600;
  color: var(--on-surface);
}

.status-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--success);
  box-shadow: 0 0 10px var(--success);
  animation: pulse 2s ease-in-out infinite;
}

@keyframes pulse {
  50% { opacity: 0.4; }
}

.task-titleblock {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.task-direction {
  color: var(--on-surface-variant);
  opacity: 0.6;
  font-size: 10px;
}

.queue-container {
  display: flex;
  gap: 24px;
  flex-grow: 1;
  overflow-y: auto;
  padding-bottom: 24px;
}

.queue-section {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.queue-section h2 {
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--on-surface);
  border-bottom: 1px solid var(--glass-border);
  padding-bottom: 8px;
}

.task-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.task-card {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.task-info {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

.task-info h4 {
  margin: 0;
  font-size: 1rem;
  color: var(--text-primary);
}

.badge {
  padding: 4px 10px;
  border-radius: var(--r-full);
  font-family: 'Geist', 'Inter', sans-serif;
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.badge.pending { background: rgba(245, 191, 0, 0.15); color: var(--warning); border: 1px solid rgba(245, 191, 0, 0.25); }
.badge.uploading, .badge.downloading { background: rgba(214, 186, 255, 0.15); color: var(--secondary); border: 1px solid rgba(214, 186, 255, 0.25); }
.badge.completed, .badge.success { background: rgba(34, 197, 94, 0.12); color: var(--success); border: 1px solid rgba(34, 197, 94, 0.25); }
.badge.failed { background: rgba(255, 180, 171, 0.12); color: var(--error); border: 1px solid rgba(255, 180, 171, 0.25); }

.progress-bar-container {
  width: 100%;
  height: 8px;
  background: var(--surface-container-highest);
  border-radius: var(--r-full);
  overflow: hidden;
}

.progress-bar {
  height: 100%;
  background: var(--primary);
  border-radius: var(--r-full);
  transition: width 0.3s ease;
}

.task-meta {
  display: flex;
  justify-content: space-between;
  font-size: 0.8rem;
  color: var(--text-secondary);
  text-transform: capitalize;
}

.error-msg {
  font-size: 0.8rem;
  color: #f87171;
  background: rgba(239, 68, 68, 0.1);
  padding: 8px;
  border-radius: 4px;
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

@media (max-width: 768px) {
  .queue-container {
    flex-direction: column;
  }
}
</style>
