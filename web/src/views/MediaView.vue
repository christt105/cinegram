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

        <!-- Backup status filter for Jellyfin libraries -->
        <div v-if="props.type !== 'telegram'" class="segmented">
          <button :class="{ active: backupFilter === 'all' }" @click="backupFilter = 'all'">All</button>
          <button :class="{ active: backupFilter === 'backed_up' }" @click="backupFilter = 'backed_up'">Backed up</button>
          <button :class="{ active: backupFilter === 'not_backed_up' }" @click="backupFilter = 'not_backed_up'">Not backed up</button>
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
          Refresh
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

    <template v-else>
      <div class="media-grid">
        <MediaCard
          v-for="item in pagedItems"
          :key="item.id"
          :media="item"
          :show-collection-count="props.type === 'telegram'"
          @delete="handleDelete"
          @download-all="handleDownloadAll"
          @download-season="handleDownloadSeason"
          @download-episode="handleDownloadEpisode"
          @upload="handleUpload"
        />
      </div>
      <div ref="sentinel" class="scroll-sentinel">
        <div v-if="hasMore" class="spinner"></div>
      </div>
    </template>

    <!-- TMDB Add Media Modal -->
    <BaseModal v-if="addMediaModalOpen" title="Add Movie or Series Manually" @close="addMediaModalOpen = false">
      <TmdbSearchPanel media-type="multi" initial-query="" select-label="Add" :select-disabled="addingMedia" @select="selectTMDBResult" />

      <template #footer>
        <button @click="addMediaModalOpen = false" class="glass-button">Close</button>
      </template>
    </BaseModal>
  </div>
</template>

<script lang="ts">
const PAGE_SIZE = 48;

type SortBy = 'latest_added' | 'popular' | 'alphabetical';
type TelegramFilter = 'all' | 'movies' | 'series';
type BackupFilter = 'all' | 'backed_up' | 'not_backed_up';

interface ViewState {
  sortBy: SortBy;
  telegramFilter: TelegramFilter;
  backupFilter: BackupFilter;
  visibleCount: number;
}

// Remembers the filters and how many cards were revealed per library type, so
// returning to a list restores the same view and its saved scroll position.
const viewStateCache = new Map<string, ViewState>();

function getViewState(type: string): ViewState {
  return viewStateCache.get(type) ?? {
    sortBy: 'latest_added',
    telegramFilter: 'all',
    backupFilter: 'all',
    visibleCount: PAGE_SIZE,
  };
}
</script>

<script setup lang="ts">
import { apiUrl } from '../config';
import { computed, ref, onMounted, onBeforeUnmount, nextTick, watch } from 'vue';
import { RefreshCw, AlertCircle, Plus } from 'lucide-vue-next';
import MediaCard from '../components/MediaCard.vue';
import BaseModal from '../components/BaseModal.vue';
import TmdbSearchPanel from '../components/tmdb/TmdbSearchPanel.vue';
import type { TmdbSearchResult } from '../composables/useTmdbSearch';
import { normalizeText } from '../utils/normalize';

const props = defineProps<{
  type: 'movies' | 'series' | 'telegram';
  searchQuery: string;
  jellyfinItems: any[];
  telegramMovies: any[];
  telegramSeries: any[];
  loading: boolean;
  jellyfinError: string | null;
  catalogError: string | null;
}>();

const emit = defineEmits(['refresh']);

const saved = getViewState(props.type);
const sortBy = ref<SortBy>(saved.sortBy);
const telegramFilter = ref<TelegramFilter>(saved.telegramFilter);
const backupFilter = ref<BackupFilter>(saved.backupFilter);

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
    return props.catalogError;
  } else {
    return props.jellyfinError;
  }
});

// Cross-reference Jellyfin and Telegram movies/series
const computedItems = computed(() => {
  let list: any[] = [];

  // Index by TMDB id so cross-referencing stays O(n) on large libraries.
  const jellyfinByTmdb = new Map<number, any>();
  for (const ji of props.jellyfinItems || []) {
    if (ji.tmdbId != null) jellyfinByTmdb.set(ji.tmdbId, ji);
  }
  const telegramMovieByTmdb = new Map<number, any>();
  for (const tm of props.telegramMovies || []) {
    if (tm.tmdb_id != null) telegramMovieByTmdb.set(tm.tmdb_id, tm);
  }
  const telegramSeriesByTmdb = new Map<number, any>();
  for (const ts of props.telegramSeries || []) {
    if (ts.tmdb_id != null) telegramSeriesByTmdb.set(ts.tmdb_id, ts);
  }

  if (props.type === 'telegram') {
    // Show only what's on Telegram (Movies & Series)
    let moviesList: any[] = [];
    if (telegramFilter.value === 'all' || telegramFilter.value === 'movies') {
      moviesList = (props.telegramMovies || []).map(tm => {
        const jItem = tm.tmdb_id != null ? jellyfinByTmdb.get(tm.tmdb_id) : undefined;
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
          collectionCount: (tm.collections || []).length,
          dateCreated: tm.created_at || '',
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
        const jItem = ts.tmdb_id != null ? jellyfinByTmdb.get(ts.tmdb_id) : undefined;
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
          collectionCount: collections.length,
          dateCreated: ts.created_at || '',
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
      const tmMatch = item.type === 'movie' && item.tmdbId != null ? telegramMovieByTmdb.get(item.tmdbId) : undefined;
      const tsMatch = item.type === 'series' && item.tmdbId != null ? telegramSeriesByTmdb.get(item.tmdbId) : undefined;
      const isTelegramMatch = item.type === 'movie' ? !!tmMatch : !!tsMatch;
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

  // 2. Backup status filter (Jellyfin libraries only)
  if (props.type !== 'telegram') {
    if (backupFilter.value === 'backed_up') {
      list = list.filter(item => item.isOnTelegram);
    } else if (backupFilter.value === 'not_backed_up') {
      list = list.filter(item => !item.isOnTelegram);
    }
  }

  // 3. Sorting
  if (sortBy.value === 'latest_added') {
    list.sort((a, b) => {
      const timeA = a.dateCreated ? new Date(a.dateCreated).getTime() : NaN;
      const timeB = b.dateCreated ? new Date(b.dateCreated).getTime() : NaN;
      const validA = !Number.isNaN(timeA);
      const validB = !Number.isNaN(timeB);
      if (validA && validB) return timeB - timeA;
      if (validA) return -1;
      if (validB) return 1;
      return (b.year || 0) - (a.year || 0);
    });
  } else if (sortBy.value === 'popular') {
    list.sort((a, b) => (b.rating || 0) - (a.rating || 0));
  } else if (sortBy.value === 'alphabetical') {
    list.sort((a, b) => (a.title || '').localeCompare(b.title || ''));
  }

  return list;
});

// Infinite scroll: reveal cards in chunks as a sentinel scrolls into view,
// keeping the DOM small on large libraries.
const visibleCount = ref(saved.visibleCount);
const pagedItems = computed(() => computedItems.value.slice(0, visibleCount.value));
const hasMore = computed(() => visibleCount.value < computedItems.value.length);
const sentinel = ref<HTMLElement | null>(null);
let observer: IntersectionObserver | null = null;

// Persist filters + revealed count so back-navigation restores the exact view.
const persist = () => {
  viewStateCache.set(props.type, {
    sortBy: sortBy.value,
    telegramFilter: telegramFilter.value,
    backupFilter: backupFilter.value,
    visibleCount: visibleCount.value,
  });
};
watch([sortBy, telegramFilter, backupFilter], persist);

const loadMore = () => {
  if (!hasMore.value) return;
  visibleCount.value = Math.min(visibleCount.value + PAGE_SIZE, computedItems.value.length);
  persist();
};

const onIntersect = async (entries: IntersectionObserverEntry[]) => {
  if (!entries[0].isIntersecting || !hasMore.value) return;
  loadMore();
  await nextTick();
  // Keep filling while the sentinel stays on screen (tall viewport / short list).
  if (observer && sentinel.value) {
    observer.unobserve(sentinel.value);
    observer.observe(sentinel.value);
  }
};

onMounted(() => {
  observer = new IntersectionObserver(onIntersect, { rootMargin: '600px' });
  if (sentinel.value) observer.observe(sentinel.value);
});

onBeforeUnmount(() => observer?.disconnect());

// The sentinel only exists once the list has items, which lands after the
// async fetch resolves — later than onMounted. Track the ref so it gets
// observed whenever it appears (and unobserved when it goes away).
watch(sentinel, (el, prev) => {
  if (!observer) return;
  if (prev) observer.unobserve(prev);
  if (el) observer.observe(el);
});

// The movies/series/telegram routes reuse this component, so react to type
// changes by restoring that type's saved view instead of remounting.
watch(() => props.type, (type) => {
  const state = getViewState(type);
  sortBy.value = state.sortBy;
  telegramFilter.value = state.telegramFilter;
  backupFilter.value = state.backupFilter;
  visibleCount.value = state.visibleCount;
});

const handleDelete = async (item: any) => {
  if (!confirm(`Are you sure you want to delete "${item.title}" from Telegram backups (database records)?`)) {
    return;
  }
  try {
    const rawId = item.rawId;
    const type = item.type;
    const url = type === 'series' 
      ? `${apiUrl}/series/${rawId}`
      : `${apiUrl}/movies/${rawId}`;
      
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
      url = `${apiUrl}/downloads/enqueue/series/${media.rawId}`;
    } else {
      if (!media.collections || media.collections.length === 0) {
        throw new Error('No files found for this movie.');
      }
      url = `${apiUrl}/downloads/enqueue/collection/${media.collections[0].id}`;
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
    const url = `${apiUrl}/downloads/enqueue/series/${event.seriesId}/season/${event.seasonNumber}`;
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
    const url = `${apiUrl}/downloads/enqueue/series/${event.seriesId}/season/${event.seasonNumber}/episode/${event.episodeNumber}`;
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
    const url = `${apiUrl}/uploads/enqueue`;
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
const addingMedia = ref(false);

const selectTMDBResult = async (result: TmdbSearchResult) => {
  addingMedia.value = true;
  try {
    const res = await fetch(`${apiUrl}/maintenance/create-media`, {
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
      emit('refresh'); // trigger library refresh
    } else {
      const txt = await res.text();
      alert("Error adding: " + txt);
    }
  } catch (err) {
    console.error(err);
    alert("Connection error.");
  } finally {
    addingMedia.value = false;
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

.scroll-sentinel {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  min-height: 1px;
  padding: var(--sp-md) 0;
}
</style>
