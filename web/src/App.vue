<template>
  <div class="dashboard">
    <aside class="sidebar glass-panel">
      <div class="logo">
        <div class="logo-icon">CG</div>
        <h2>Cinegram</h2>
      </div>
      
      <nav class="nav-menu">
        <router-link to="/movies" class="nav-item" active-class="active">
          <Film :size="20" />
          <span>Movies</span>
        </router-link>
        <router-link to="/series" class="nav-item" active-class="active">
          <Tv :size="20" />
          <span>Series</span>
        </router-link>
        <router-link to="/telegram" class="nav-item" active-class="active">
          <Send :size="20" />
          <span>Telegram Library</span>
        </router-link>
        <router-link to="/queue" class="nav-item" active-class="active">
          <List :size="20" />
          <span>Transfers</span>
        </router-link>
        <router-link to="/settings" class="nav-item" active-class="active">
          <Settings :size="20" />
          <span>Settings</span>
        </router-link>
      </nav>
      
      <div class="server-status">
        <div class="status-indicator"></div>
        <span>Connected to Jellyfin & Backend</span>
      </div>
    </aside>
    
    <main class="main-content">
      <header class="header">
        <div class="search-bar glass-panel" v-if="!['settings', 'item-detail', 'queue'].includes($route.name as string)">
          <Search :size="18" class="search-icon" />
          <input type="text" v-model="searchQuery" placeholder="Search for movies, series..." />
        </div>
        <div v-else style="flex-grow: 1;"></div>
        
        <div class="user-profile glass-panel">
          <User :size="18" />
        </div>
      </header>
      
      <router-view 
        :searchQuery="searchQuery"
        :jellyfinItems="jellyfinItems"
        :telegramMovies="telegramMovies"
        :telegramSeries="telegramSeries"
        :loading="loading"
        :jellyfinError="jellyfinError"
        :backendError="backendError"
        @refresh="fetchItems"
      />
    </main>
  </div>
</template>

<script setup lang="ts">
import { Film, Tv, Send, Settings, Search, User, List } from 'lucide-vue-next';
import { onMounted, ref, computed } from 'vue';
import { useRoute } from 'vue-router';
import { useJellyfin } from './api/jellyfin';
import { useBackend } from './api/backend';

const route = useRoute();
const searchQuery = ref('');

const { items: jellyfinItems, loading: jellyfinLoading, error: jellyfinError, fetchItems: fetchJellyfin } = useJellyfin();
const { telegramMovies, telegramSeries, loading: backendLoading, error: backendError, fetchTelegramMovies, fetchTelegramSeries } = useBackend();

const loading = computed(() => jellyfinLoading.value || backendLoading.value);

const fetchItems = () => {
  fetchJellyfin();
  fetchTelegramMovies();
  fetchTelegramSeries();
};

onMounted(() => {
  fetchItems();
});
</script>

<style>
.dashboard {
  display: flex;
  width: 100%;
  height: 100vh;
}

/* Sidebar */
.sidebar {
  width: 260px;
  margin: 16px;
  display: flex;
  flex-direction: column;
  padding: 24px 16px;
}

.logo {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 40px;
  padding: 0 8px;
}

.logo-icon {
  width: 40px;
  height: 40px;
  background: linear-gradient(135deg, var(--jellyfin-blue), var(--jellyfin-purple));
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 1.2rem;
  color: white;
  box-shadow: 0 4px 15px rgba(0, 164, 220, 0.4);
}

.logo h2 {
  font-weight: 600;
  font-size: 1.5rem;
  letter-spacing: -0.5px;
}

.nav-menu {
  display: flex;
  flex-direction: column;
  gap: 8px;
  flex-grow: 1;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border-radius: 8px;
  color: var(--text-secondary);
  text-decoration: none;
  font-weight: 500;
  transition: all 0.2s ease;
}

.nav-item:hover {
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-primary);
}

.nav-item.active {
  background: rgba(170, 92, 195, 0.15);
  color: var(--text-primary);
  border-left: 3px solid var(--jellyfin-purple);
}

.server-status {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 16px;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 8px;
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.status-indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #22c55e;
  box-shadow: 0 0 10px #22c55e;
}

/* Main Content */
.main-content {
  flex-grow: 1;
  padding: 16px 24px 16px 8px;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 32px;
  gap: 24px;
}

.search-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 20px;
  flex-grow: 1;
  max-width: 600px;
  border-radius: 12px;
}

.search-icon {
  color: var(--text-secondary);
}

.search-bar input {
  background: transparent;
  border: none;
  color: var(--text-primary);
  font-family: inherit;
  font-size: 1rem;
  width: 100%;
  outline: none;
}

.search-bar input::placeholder {
  color: var(--text-secondary);
}

.user-profile {
  width: 46px;
  height: 46px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.content-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  margin-bottom: 24px;
}

.content-header h1 {
  font-size: 2rem;
  font-weight: 600;
  letter-spacing: -0.5px;
}

.filters {
  display: flex;
  gap: 24px;
}

.media-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 24px;
  padding-bottom: 40px;
}

.state-message {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 200px;
  gap: 16px;
  color: var(--text-secondary);
}

.state-message.error {
  color: #ef4444;
}

.spinner {
  width: 40px;
  height: 40px;
  border: 3px solid rgba(255, 255, 255, 0.1);
  border-radius: 50%;
  border-top-color: var(--jellyfin-purple);
  animation: spin 1s ease-in-out infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.local-warning {
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.3);
  padding: 12px 16px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 24px;
  color: #f87171;
  font-size: 0.9rem;
}

/* Settings styling */
.settings-container {
  padding: 8px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.settings-container h1 {
  font-size: 2rem;
  font-weight: 600;
  letter-spacing: -0.5px;
}

.settings-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 24px;
}

.settings-card {
  padding: 24px;
  border-radius: 12px;
}

.settings-card h3 {
  margin-bottom: 20px;
  font-size: 1.2rem;
  font-weight: 600;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  padding-bottom: 12px;
}

.status-item {
  display: flex;
  justify-content: space-between;
  padding: 10px 0;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.status-item:last-child {
  border-bottom: none;
}

.status-item .label {
  color: var(--text-secondary);
  font-weight: 500;
}

.status-item .value {
  color: var(--text-primary);
  font-weight: 600;
}

.success-text {
  color: #22c55e;
}

.error-text {
  color: #ef4444;
}

/* Mobile Responsive Layout */
@media (max-width: 768px) {
  .dashboard {
    flex-direction: column;
  }
  
  .sidebar {
    position: fixed;
    bottom: 0;
    left: 0;
    width: 100%;
    height: 75px;
    margin: 0;
    padding: 0 16px;
    flex-direction: row;
    justify-content: space-around;
    align-items: center;
    border-radius: 20px 20px 0 0;
    border-bottom: none;
    border-left: none;
    border-right: none;
    z-index: 100;
    background: rgba(20, 22, 28, 0.95);
    box-shadow: 0 -4px 20px rgba(0, 0, 0, 0.5);
  }
  
  .logo, .server-status {
    display: none;
  }
  
  .nav-menu {
    flex-direction: row;
    justify-content: space-around;
    align-items: center;
    width: 100%;
  }
  
  .nav-item {
    flex-direction: column;
    gap: 6px;
    padding: 8px;
    border-left: none !important;
  }
  
  .nav-item span {
    font-size: 0.65rem;
    display: block;
  }
  
  .nav-item.active {
    background: transparent;
    color: var(--jellyfin-blue);
  }
  
  .main-content {
    padding: 16px;
    padding-bottom: 90px; /* Space for bottom nav */
  }
  
  .header {
    flex-direction: row;
    margin-bottom: 24px;
  }
  
  .search-bar {
    max-width: 100%;
  }
  
  .user-profile {
    display: none;
  }
  
  .content-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 16px;
  }
  
  .filters {
    width: 100%;
    overflow-x: auto;
    padding-bottom: 8px;
    white-space: nowrap;
    gap: 16px;
  }
  
  .media-grid {
    grid-template-columns: repeat(auto-fill, minmax(130px, 1fr));
    gap: 12px;
  }
}

.glass-button.active {
  background: var(--jellyfin-purple) !important;
  border-color: var(--jellyfin-purple) !important;
  box-shadow: 0 0 10px rgba(170, 92, 195, 0.4);
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
.spinning {
  animation: spin 1s linear infinite;
}
</style>
