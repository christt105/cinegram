<template>
  <div class="media-view">
    <div class="content-header">
      <h1>{{ title }}</h1>
      <div class="filters">
        <!-- Type filters for Telegram Library -->
        <template v-if="props.type === 'telegram'">
          <button class="glass-button" :class="{ active: telegramFilter === 'all' }" @click="telegramFilter = 'all'">Todo</button>
          <button class="glass-button" :class="{ active: telegramFilter === 'movies' }" @click="telegramFilter = 'movies'">Películas</button>
          <button class="glass-button" :class="{ active: telegramFilter === 'series' }" @click="telegramFilter = 'series'">Series</button>
          <div class="filter-separator"></div>
        </template>

        <button class="glass-button" :class="{ active: sortBy === 'latest_added' }" @click="sortBy = 'latest_added'">Últimos añadidos</button>
        <button class="glass-button" :class="{ active: sortBy === 'popular' }" @click="sortBy = 'popular'">Más popular</button>
        <button class="glass-button" :class="{ active: sortBy === 'alphabetical' }" @click="sortBy = 'alphabetical'">Orden alfabético</button>
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
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { RefreshCw, AlertCircle } from 'lucide-vue-next';
import MediaCard from '../components/MediaCard.vue';

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
          dateCreated: jItem ? jItem.dateCreated : `tg-${tm.id}`,
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
          dateCreated: jItem ? jItem.dateCreated : `tg-${ts.id}`,
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
    const q = props.searchQuery.toLowerCase().trim();
    list = list.filter(item => item.title && item.title.toLowerCase().includes(q));
  }

  // 2. Sorting
  if (sortBy.value === 'latest_added') {
    list.sort((a, b) => {
      if (props.type === 'telegram' && a.rawId && b.rawId) {
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
</script>
