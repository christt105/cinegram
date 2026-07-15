<template>
  <div class="dashboard">
    <aside class="sidebar glass-panel">
      <div class="logo">
        <div class="logo-icon">JG</div>
        <h2>Jellygram</h2>
      </div>
      
      <nav class="nav-menu">
        <a href="/movies" class="nav-item" :class="{ active: activeTab === 'movies' }" @click.prevent="activeTab = 'movies'">
          <Film :size="20" />
          <span>Movies</span>
        </a>
        <a href="/series" class="nav-item" :class="{ active: activeTab === 'series' }" @click.prevent="activeTab = 'series'">
          <Tv :size="20" />
          <span>Series</span>
        </a>
        <a href="/telegram" class="nav-item" :class="{ active: activeTab === 'telegram' }" @click.prevent="activeTab = 'telegram'">
          <Send :size="20" />
          <span>Telegram Library</span>
        </a>
        <a href="/settings" class="nav-item" :class="{ active: activeTab === 'settings' }" @click.prevent="activeTab = 'settings'">
          <Settings :size="20" />
          <span>Settings</span>
        </a>
      </nav>
      
      <div class="server-status">
        <div class="status-indicator"></div>
        <span>Connected to Jellyfin & Backend</span>
      </div>
    </aside>
    
    <main class="main-content">
      <header class="header">
        <div class="search-bar glass-panel" v-if="activeTab !== 'settings'">
          <Search :size="18" class="search-icon" />
          <input type="text" v-model="searchQuery" placeholder="Search for movies, series..." />
        </div>
        <div v-else style="flex-grow: 1;"></div>
        
        <div class="user-profile glass-panel">
          <User :size="18" />
        </div>
      </header>
      
      <!-- Settings Panel -->
      <div v-if="activeTab === 'settings'" class="settings-container">
        <h1>System Settings</h1>
        
        <div class="settings-grid">
          <!-- Connection Status Card -->
          <div class="glass-panel settings-card">
            <h3>Connection Status</h3>
            <div class="status-item">
              <span class="label">Jellyfin URL:</span>
              <span class="value">{{ jellyfinUrl }}</span>
            </div>
            <div class="status-item">
              <span class="label">Jellyfin Auth:</span>
              <span class="value success-text" v-if="jellyfinItems.length > 0">Authenticated</span>
              <span class="value error-text" v-else>Disconnected / Missing Token</span>
            </div>
            <div class="status-item">
              <span class="label">Backend API URL:</span>
              <span class="value">{{ backendUrl }}</span>
            </div>
            <div class="status-item">
              <span class="label">Telegram Bot Username:</span>
              <span class="value">@BibliotecaKachopinesBot</span>
            </div>
          </div>
          
          <!-- Library Statistics Card -->
          <div class="glass-panel settings-card">
            <h3>Library Statistics</h3>
            <div class="status-item">
              <span class="label">Movies in Telegram:</span>
              <span class="value">{{ telegramMovies.length }}</span>
            </div>
            <div class="status-item">
              <span class="label">Series in Telegram:</span>
              <span class="value">{{ telegramSeries.length }}</span>
            </div>
            <div class="status-item">
              <span class="label">Total Jellyfin Items:</span>
              <span class="value">{{ jellyfinItems.length }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Media Content Views -->
      <div v-else>
        <div class="content-header">
          <h1>{{ activeTab === 'telegram' ? 'Telegram Library' : 'Discover Media' }}</h1>
          <div class="filters">
            <button class="glass-button" :class="{ active: sortBy === 'latest_added' }" @click="sortBy = 'latest_added'">Últimos añadidos</button>
            <button class="glass-button" :class="{ active: sortBy === 'popular' }" @click="sortBy = 'popular'">Más popular</button>
            <button class="glass-button" :class="{ active: sortBy === 'alphabetical' }" @click="sortBy = 'alphabetical'">Orden alfabético</button>
            <button class="glass-button primary" @click="fetchItems" :disabled="loading">
              <RefreshCw :size="16" :class="{ spinning: loading }" />
              Sync Jellyfin
            </button>
          </div>
        </div>
        
        <!-- Local Errors (graceful, non-blocking) -->
        <div v-if="activeError" class="local-warning glass-panel">
          <AlertCircle :size="18" />
          <span>{{ activeError }}</span>
        </div>

        <div v-if="loading && computedItems.length === 0" class="state-message">
          <div class="spinner"></div>
          <p>Loading media...</p>
        </div>

        <div v-else-if="computedItems.length === 0" class="state-message">
          <p>No media found matching the filters or search query.</p>
        </div>

        <div v-else class="media-grid">
          <MediaCard 
            v-for="item in computedItems" 
            :key="item.id" 
            :media="item" 
            @delete="handleDelete"
            @download-all="handleDownloadAll"
            @download-season="handleDownloadSeason"
            @download-episode="handleDownloadEpisode"
            @upload="handleUpload"
          />
        </div>
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { Film, Tv, Send, Settings, Search, User, RefreshCw, AlertCircle } from 'lucide-vue-next';
import MediaCard from './components/MediaCard.vue';
import { onMounted, ref, computed, watch } from 'vue';
import { useJellyfin } from './api/jellyfin';
import { useBackend } from './api/backend';

// Tab selection from pathname (no hash, clean URL!)
const getTabFromPath = (): 'movies' | 'series' | 'telegram' | 'settings' => {
  const path = window.location.pathname.replace(/^\/+/, '');
  if (['movies', 'series', 'telegram', 'settings'].includes(path)) {
    return path as any;
  }
  return 'movies';
};

const activeTab = ref(getTabFromPath());

// Redirect "/" to "/movies"
if (window.location.pathname === '/') {
  window.history.replaceState({}, '', '/movies');
}

watch(activeTab, (newTab) => {
  const newPath = '/' + newTab;
  if (window.location.pathname !== newPath) {
    window.history.pushState({}, '', newPath);
  }
});

window.addEventListener('popstate', () => {
  activeTab.value = getTabFromPath();
});

const searchQuery = ref('');
const sortBy = ref<'latest_added' | 'popular' | 'alphabetical'>('latest_added');

const { items: jellyfinItems, loading: jellyfinLoading, error: jellyfinError, fetchItems: fetchJellyfin } = useJellyfin();
const { telegramMovies, telegramSeries, loading: backendLoading, error: backendError, fetchTelegramMovies, fetchTelegramSeries } = useBackend();

const jellyfinUrl = import.meta.env.VITE_JELLYFIN_URL || 'http://192.168.1.15:8096';
const backendUrl = import.meta.env.VITE_BACKEND_URL || `${window.location.protocol}//${window.location.hostname}:8005`;
const loading = computed(() => jellyfinLoading.value || backendLoading.value);

// Graceful, tab-specific warnings instead of breaking the entire app
const activeError = computed(() => {
  if (activeTab.value === 'telegram') {
    return backendError.value;
  } else {
    return jellyfinError.value;
  }
});

// Cross-reference Jellyfin and Telegram movies/series
const computedItems = computed(() => {
  let list: any[] = [];

  if (activeTab.value === 'telegram') {
    // Show only what's on Telegram (Movies & Series)
    const moviesList = telegramMovies.value.map(tm => {
      const jItem = jellyfinItems.value.find(ji => ji.tmdbId === tm.tmdb_id);
      return {
        id: `tg-movie-${tm.id}`,
        rawId: tm.id,
        title: tm.title,
        year: tm.release_year,
        type: 'movie' as const,
        synopsis: tm.overview || 'No synopsis available.',
        coverUrl: tm.poster_path ? `https://image.tmdb.org/t/p/w500${tm.poster_path}` : '',
        resolutions: tm.collections.map(c => c.quality).filter(Boolean),
        tags: [],
        tmdbId: tm.tmdb_id,
        isOnTelegram: true,
        isInJellyfin: !!jItem,
        collections: tm.collections,
        dateCreated: jItem ? jItem.dateCreated : `tg-${tm.id}`,
        rating: jItem ? (jItem.rating || 0) : 0
      };
    });

    const seriesList = telegramSeries.value.map(ts => {
      const collections: any[] = [];
      ts.seasons.forEach(s => {
        collections.push(...s.collections);
        s.episodes.forEach(e => {
          collections.push(...e.collections);
        });
      });
      const jItem = jellyfinItems.value.find(ji => ji.tmdbId === ts.tmdb_id);
      return {
        id: `tg-series-${ts.id}`,
        rawId: ts.id,
        title: ts.manual_title,
        year: ts.release_year,
        type: 'series' as const,
        synopsis: ts.overview || 'TV Show backed up on Telegram.',
        coverUrl: ts.poster_path ? `https://image.tmdb.org/t/p/w500${ts.poster_path}` : '',
        resolutions: [...new Set(collections.map(c => c.quality).filter(Boolean))],
        tags: [],
        tmdbId: ts.tmdb_id,
        isOnTelegram: true,
        isInJellyfin: !!jItem,
        seasons: ts.seasons,
        dateCreated: jItem ? jItem.dateCreated : `tg-${ts.id}`,
        rating: jItem ? (jItem.rating || 0) : 0
      };
    });

    list = [...moviesList, ...seriesList];
  } else {
    // Filter Jellyfin items by type
    const filteredJellyfin = jellyfinItems.value.filter(item => {
      if (activeTab.value === 'movies') return item.type === 'movie';
      if (activeTab.value === 'series') return item.type === 'series';
      return true;
    });

    list = filteredJellyfin.map(item => {
      const isTelegramMatch = item.type === 'movie'
        ? telegramMovies.value.some(tm => tm.tmdb_id === item.tmdbId)
        : telegramSeries.value.some(ts => ts.tmdb_id === item.tmdbId);
      const tmMatch = item.type === 'movie' ? telegramMovies.value.find(tm => tm.tmdb_id === item.tmdbId) : undefined;
      const tsMatch = item.type === 'series' ? telegramSeries.value.find(ts => ts.tmdb_id === item.tmdbId) : undefined;
      return {
        ...item,
        rawId: tmMatch ? tmMatch.id : (tsMatch ? tsMatch.id : undefined),
        collections: tmMatch ? tmMatch.collections : undefined,
        seasons: tsMatch ? tsMatch.seasons : undefined,
        isOnTelegram: isTelegramMatch,
        isInJellyfin: true
      };
    });
  }

  // 1. Search Query Filter
  if (searchQuery.value.trim() !== '') {
    const q = searchQuery.value.toLowerCase().trim();
    list = list.filter(item => item.title && item.title.toLowerCase().includes(q));
  }

  // 2. Sorting
  if (sortBy.value === 'latest_added') {
    list.sort((a, b) => {
      if (activeTab.value === 'telegram' && a.rawId && b.rawId) {
        return b.rawId - a.rawId;
      }
      const dateA = a.dateCreated || '';
      const dateB = b.dateCreated || '';
      if (dateA && dateB) {
        return dateB.localeCompare(dateA);
      }
      return (b.year || 0) - (a.year || 0);
    });
  } else if (sortBy.value === 'popular') {
    list.sort((a, b) => (b.rating || 0) - (a.rating || 0));
  } else if (sortBy.value === 'alphabetical') {
    list.sort((a, b) => (a.title || '').localeCompare(b.title || ''));
  }

  return list;
});

const handleDelete = async (item: any) => {
  if (!confirm(`Are you sure you want to delete "${item.title}" from Telegram backups (database records)?`)) {
    return;
  }
  try {
    const rawId = item.rawId;
    const type = item.type;
    const url = type === 'series' 
      ? `${backendUrl}/series/${rawId}`
      : `${backendUrl}/movies/${rawId}`;
      
    const res = await fetch(url, { method: 'DELETE' });
    if (!res.ok) {
      throw new Error(`Failed to delete: ${res.statusText}`);
    }
    fetchItems();
  } catch (e: any) {
    console.error(e);
    alert(e.message || 'Error deleting backup');
  }
};

const handleDownloadAll = async (media: any) => {
  try {
    let url = '';
    if (media.type === 'series') {
      url = `${backendUrl}/downloads/enqueue/series/${media.rawId}`;
    } else {
      if (!media.collections || media.collections.length === 0) {
        throw new Error('No files found for this movie.');
      }
      url = `${backendUrl}/downloads/enqueue/collection/${media.collections[0].id}`;
    }
    const res = await fetch(url, { method: 'POST' });
    if (!res.ok) throw new Error('Error enqueuing download.');
    alert(`Descarga encolada para: ${media.title}`);
  } catch (e: any) {
    console.error(e);
    alert(e.message || 'Error enqueuing download');
  }
};

const handleDownloadSeason = async (event: { seriesId: number, seasonNumber: number, title: string }) => {
  try {
    const url = `${backendUrl}/downloads/enqueue/series/${event.seriesId}/season/${event.seasonNumber}`;
    const res = await fetch(url, { method: 'POST' });
    if (!res.ok) throw new Error('Error enqueuing download.');
    alert(`Descarga encolada para Temporada ${event.seasonNumber} de ${event.title}`);
  } catch (e: any) {
    console.error(e);
    alert(e.message || 'Error enqueuing download');
  }
};

const handleDownloadEpisode = async (event: { seriesId: number, seasonNumber: number, episodeNumber: number, title: string }) => {
  try {
    const url = `${backendUrl}/downloads/enqueue/series/${event.seriesId}/season/${event.seasonNumber}/episode/${event.episodeNumber}`;
    const res = await fetch(url, { method: 'POST' });
    if (!res.ok) throw new Error('Error enqueuing download.');
    alert(`Descarga encolada para S${event.seasonNumber.toString().padStart(2, '0')}E${event.episodeNumber.toString().padStart(2, '0')} de ${event.title}`);
  } catch (e: any) {
    console.error(e);
    alert(e.message || 'Error enqueuing download');
  }
};

const handleUpload = async (media: any) => {
  if (!media.path) {
    alert('No se pudo encontrar la ruta local del archivo en Jellyfin.');
    return;
  }
  try {
    const url = `${backendUrl}/uploads/enqueue`;
    const payload = {
      jellyfin_id: media.id,
      tmdb_id: media.tmdbId,
      media_type: media.type,
      path: media.path,
      title: media.title,
      year: media.year
    };
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    if (!res.ok) throw new Error('Error al encolar la subida.');
    alert(`Subida encolada para: ${media.title}`);
  } catch (e: any) {
    console.error(e);
    alert(e.message || 'Error enqueuing upload');
  }
};

const fetchItems = () => {
  fetchJellyfin();
  fetchTelegramMovies();
  fetchTelegramSeries();
};

onMounted(() => {
  fetchItems();
});
</script>

<style scoped>
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
  gap: 12px;
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
  }
  
  .media-grid {
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 16px;
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
