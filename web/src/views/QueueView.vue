<template>
  <div class="queue-view">
    <div class="content-header">
      <h1>Active Transfers</h1>
      <button class="glass-button primary" @click="fetchQueues">
        <RefreshCw :size="16" :class="{ spinning: isLoading }" />
        Refresh
      </button>
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
              <h4>{{ task.title || 'Unknown Media' }} ({{ task.year || '' }})</h4>
              <span class="badge" :class="task.status">{{ task.status }}</span>
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
              <h4>Collection ID: {{ task.collection_id }}</h4>
              <span class="badge" :class="task.status">{{ task.status }}</span>
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
import { ref, onMounted, onUnmounted } from 'vue';
import { RefreshCw } from 'lucide-vue-next';

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
let pollInterval: any = null;

const fetchQueues = async () => {
  isLoading.value = true;
  try {
    const backendUrl = import.meta.env.VITE_BACKEND_URL || 'http://192.168.1.15:8005';
    
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
  color: var(--text-secondary);
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
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.badge.pending { background: rgba(255, 170, 0, 0.2); color: #fbbf24; }
.badge.uploading, .badge.downloading { background: rgba(59, 130, 246, 0.2); color: #60a5fa; }
.badge.failed { background: rgba(239, 68, 68, 0.2); color: #f87171; }

.progress-bar-container {
  width: 100%;
  height: 6px;
  background: rgba(0, 0, 0, 0.3);
  border-radius: 3px;
  overflow: hidden;
}

.progress-bar {
  height: 100%;
  background: var(--jellyfin-blue);
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
