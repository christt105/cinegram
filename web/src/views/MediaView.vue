<template>
  <div class="media-view">
    <div class="content-header">
      <div class="content-heading">
        <h1>{{ title }}</h1>
        <p class="content-subtitle">{{ subtitle }}</p>
      </div>
      <div class="filters">
        <!-- Type filters for Telegram Library -->
        <div v-if="props.type === 'telegram'" class="segmented">
          <button :class="{ active: telegramFilter === 'all' }" @click="telegramFilter = 'all'">All</button>
          <button :class="{ active: telegramFilter === 'movies' }" @click="telegramFilter = 'movies'">Movies</button>
          <button :class="{ active: telegramFilter === 'series' }" @click="telegramFilter = 'series'">Series</button>
        </div>

        <div class="segmented">
          <button :class="{ active: sortBy === 'latest_added' }" @click="sortBy = 'latest_added'">Latest</button>
          <button :class="{ active: sortBy === 'popular' }" @click="sortBy = 'popular'">Popular</button>
          <button :class="{ active: sortBy === 'alphabetical' }" @click="sortBy = 'alphabetical'">A-Z</button>
        </div>

        <button class="glass-button" @click="addMediaModalOpen = true" :disabled="loading">
          <Plus :size="16" /> Add Manually
        </button>
        <button class="glass-button primary" @click="emit('refresh')" :disabled="loading">
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

    <!-- TMDB Add Media Modal -->
    <div v-if="addMediaModalOpen" class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px); padding: 1rem;">
      <div class="glass-panel" style="width: 100%; max-width: 600px; max-height: 85vh; display: flex; flex-direction: column; gap: 1rem; padding: 1.5rem; background: var(--glass-bg-strong); border: 1px solid var(--glass-border); border-radius: 16px; overflow: hidden; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5); backdrop-filter: blur(24px);">
        
        <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">
          <h3 style="margin: 0; font-size: 1.2rem; color: #fff;">Add Movie or Series Manually</h3>
          <button @click="addMediaModalOpen = false" class="glass-button icon-only" style="padding: 0; border-radius: 50%; width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-size: 14px;">✕</button>
        </div>
        
        <!-- Search Bar -->
        <div style="display: flex; gap: 0.5rem; width: 100%;">
          <input v-model="searchQueryTMDB" @keyup.enter="searchTMDB" type="text" placeholder="Type the series or movie name..." style="flex-grow: 1; padding: 10px 14px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-size: 0.95rem;" />
          <button @click="searchTMDB" class="glass-button primary" style="padding: 0 1.25rem;">Search</button>
        </div>

        <!-- Results List -->
        <div style="flex-grow: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 0.75rem; padding-right: 0.25rem;">
          <div v-if="isSearchingTMDB" style="text-align: center; padding: 2rem; color: #a1a1aa;">
            <div style="margin-bottom: 0.5rem;">Searching...</div>
          </div>
          <div v-else-if="searchResultsTMDB.length === 0 && searchQueryTMDB" style="text-align: center; padding: 2rem; color: #a1a1aa;">
            No results found.
          </div>
          
          <div v-for="result in searchResultsTMDB" :key="result.id" class="result-card" style="display: flex; gap: 1rem; padding: 0.75rem; background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.05); border-radius: 10px; transition: background 0.2s;">
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
            
            <button @click="selectTMDBResult(result)" class="glass-button" style="align-self: center; background: rgba(16, 185, 129, 0.15); border-color: rgba(16, 185, 129, 0.35); color: #a7f3d0; padding: 6px 12px; font-size: 0.8rem; border-radius: 6px; flex-shrink: 0;">
              Add
            </button>
          </div>
        </div>
        
        <div style="display: flex; justify-content: flex-end; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
          <button @click="addMediaModalOpen = false" class="glass-button" style="padding: 6px 16px;">Close</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { RefreshCw, AlertCircle, Plus } from 'lucide-vue-next';
import MediaCard from '../components/MediaCard.vue';
import { normalizeText } from '../utils/normalize';

const props = defineProps<{
  type: 'movies' | 'series' | 'telegram';
  searchQuery: string;
  jellyfinItems: any[];
  telegramMovies: any[];
  telegramSeries: any[];
  loading: boolean;
  jellyfinError: string | null;
  backendError: string | null;
}>();

const emit = defineEmits(['refresh']);

const sortBy = ref<'latest_added' | 'popular' | 'alphabetical'>('latest_added');
const telegramFilter = ref<'all' | 'movies' | 'series'>('all');
const backendUrl = import.meta.env.VITE_BACKEND_URL || `${window.location.protocol}//${window.location.hostname}:8005`;

const title = computed(() => {
  if (props.type === 'telegram') return 'Telegram Library';
  if (props.type === 'movies') return 'Movies';
  return 'Series';
});

const subtitle = computed(() => {
  if (props.type === 'telegram') return 'Cloud backup for your entire media collection';
  if (props.type === 'movies') return 'Manage and sync your cinematic collection';
  return 'Manage and sync your series collection';
});

const activeError = computed(() => {
  if (props.type === 'telegram') {
    return props.backendError;
  } else {
    return props.jellyfinError;
  }
});

// Cross-reference Jellyfin and Telegram movies/series
const computedItems = computed(() => {
  let list: any[] = [];

  if (props.type === 'telegram') {
    // Show only what's on Telegram (Movies & Series)
    let moviesList: any[] = [];
    if (telegramFilter.value === 'all' || telegramFilter.value === 'movies') {
      moviesList = (props.telegramMovies || []).map(tm => {
        const jItem = (props.jellyfinItems || []).find(ji => ji.tmdbId === tm.tmdb_id);
        return {
          id: `tg-movie-${tm.id}`,
          rawId: tm.id,
          title: tm.title,
          year: tm.release_year,
          type: 'movie' as const,
          synopsis: tm.overview || 'No synopsis available.',
          coverUrl: tm.poster_path ? `https://image.tmdb.org/t/p/w500${tm.poster_path}` : '',
          resolutions: tm.collections.map((c: any) => c.quality).filter(Boolean),
          tags: [],
          tmdbId: tm.tmdb_id,
          isOnTelegram: true,
          isInJellyfin: !!jItem,
          collections: tm.collections,
          dateCreated: jItem ? jItem.dateCreated : (tm.created_at || ''),
          rating: jItem ? (jItem.rating || 0) : 0
        };
      });
    }

    let seriesList: any[] = [];
    if (telegramFilter.value === 'all' || telegramFilter.value === 'series') {
      seriesList = (props.telegramSeries || []).map(ts => {
        const collections: any[] = [];
        ts.seasons.forEach((s: any) => {
          collections.push(...s.collections);
          s.episodes.forEach((e: any) => {
            collections.push(...e.collections);
          });
        });
        const jItem = (props.jellyfinItems || []).find(ji => ji.tmdbId === ts.tmdb_id);
        return {
          id: `tg-series-${ts.id}`,
          rawId: ts.id,
          title: ts.manual_title,
          year: ts.release_year,
          type: 'series' as const,
          synopsis: ts.overview || 'TV Show backed up on Telegram.',
          coverUrl: ts.poster_path ? `https://image.tmdb.org/t/p/w500${ts.poster_path}` : '',
          resolutions: [...new Set(collections.map((c: any) => c.quality).filter(Boolean))],
          tags: [],
          tmdbId: ts.tmdb_id,
          isOnTelegram: true,
          isInJellyfin: !!jItem,
          seasons: ts.seasons,
          dateCreated: jItem ? jItem.dateCreated : (ts.created_at || ''),
          rating: jItem ? (jItem.rating || 0) : 0
        };
      });
    }

    list = [...moviesList, ...seriesList];
  } else {
    // Filter Jellyfin items by type
    const filteredJellyfin = (props.jellyfinItems || []).filter(item => {
      if (props.type === 'movies') return item.type === 'movie';
      if (props.type === 'series') return item.type === 'series';
      return true;
    });

    list = filteredJellyfin.map(item => {
      const isTelegramMatch = item.type === 'movie'
        ? (props.telegramMovies || []).some(tm => tm.tmdb_id === item.tmdbId)
        : (props.telegramSeries || []).some(ts => ts.tmdb_id === item.tmdbId);
      const tmMatch = item.type === 'movie' ? (props.telegramMovies || []).find(tm => tm.tmdb_id === item.tmdbId) : undefined;
      const tsMatch = item.type === 'series' ? (props.telegramSeries || []).find(ts => ts.tmdb_id === item.tmdbId) : undefined;
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
  if (props.searchQuery && props.searchQuery.trim() !== '') {
    const q = normalizeText(props.searchQuery);
    list = list.filter(item => item.title && normalizeText(item.title).includes(q));
  }

  // 2. Sorting
  if (sortBy.value === 'latest_added') {
    list.sort((a, b) => {
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
    emit('refresh');
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
    alert(`Download queued for: ${media.title}`);
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
    alert(`Download queued for Season ${event.seasonNumber} of ${event.title}`);
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
    alert(`Download queued for S${event.seasonNumber.toString().padStart(2, '0')}E${event.episodeNumber.toString().padStart(2, '0')} of ${event.title}`);
  } catch (e: any) {
    console.error(e);
    alert(e.message || 'Error enqueuing download');
  }
};

const handleUpload = async (media: any) => {
  if (!media.path) {
    alert('Could not find the local file path in Jellyfin.');
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
    if (!res.ok) throw new Error('Error queuing the upload.');
    alert(`Upload queued for: ${media.title}`);
  } catch (e: any) {
    console.error(e);
    alert(e.message || 'Error enqueuing upload');
  }
};

const addMediaModalOpen = ref(false);
const searchQueryTMDB = ref("");
const searchResultsTMDB = ref<any[]>([]);
const isSearchingTMDB = ref(false);

const searchTMDB = async () => {
  if (!searchQueryTMDB.value.trim()) return;
  isSearchingTMDB.value = true;
  searchResultsTMDB.value = [];
  try {
    const res = await fetch(`${backendUrl}/tmdb/search?query=${encodeURIComponent(searchQueryTMDB.value.trim())}`);
    if (res.ok) {
      searchResultsTMDB.value = await res.json();
    }
  } catch (err) {
    console.error(err);
  } finally {
    isSearchingTMDB.value = false;
  }
};

const selectTMDBResult = async (result: any) => {
  try {
    const res = await fetch(`${backendUrl}/maintenance/create-media`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        tmdb_id: result.id,
        media_type: result.media_type
      })
    });
    if (res.ok) {
      alert(`"${result.title}" (${result.year}) added successfully to the local library.`);
      addMediaModalOpen.value = false;
      searchQueryTMDB.value = "";
      searchResultsTMDB.value = [];
      emit('refresh'); // trigger library refresh
    } else {
      const txt = await res.text();
      alert("Error adding: " + txt);
    }
  } catch (err) {
    console.error(err);
    alert("Connection error.");
  }
};
</script>

<style scoped>
.content-heading h1 {
  font-size: 2rem;
  font-weight: 700;
  letter-spacing: -0.01em;
}

.segmented {
  display: inline-flex;
  gap: 4px;
  padding: 4px;
  background: var(--surface-container-low);
  border: 1px solid var(--glass-border);
  border-radius: var(--r-xl);
}

.segmented button {
  padding: 6px 14px;
  border: none;
  background: transparent;
  color: var(--on-surface-variant);
  font-family: 'Geist', 'Inter', sans-serif;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  border-radius: var(--r-lg);
  cursor: pointer;
  transition: all 0.2s ease;
}

.segmented button:hover {
  color: var(--on-surface);
}

.segmented button.active {
  background: var(--primary);
  color: var(--on-primary);
}
</style>
