import { ref } from 'vue';
import { backendUrl } from '../config';

export interface TelegramFile {
  id: number;
  message_id: number;
  filename: string;
  filesize: number;
  mime_type: string;
  created_at: string;
}

export interface TelegramCollection {
  id: number;
  name: string;
  quality: string;
  files: TelegramFile[];
}

export interface BackendMovie {
  id: number;
  tmdb_id: number;
  title: string;
  poster_path: string;
  release_year: number;
  overview: string;
  collections: TelegramCollection[];
}

export interface BackendEpisode {
  id: number;
  episode_number: number;
  title: string;
  collections: TelegramCollection[];
}

export interface BackendSeason {
  id: number;
  season_number: number;
  episodes: BackendEpisode[];
  collections: TelegramCollection[];
}

export interface BackendSeries {
  id: number;
  tmdb_id: number;
  manual_title: string;
  poster_path?: string;
  overview?: string;
  release_year?: number;
  seasons: BackendSeason[];
}

export function useBackend() {
  const telegramMovies = ref<BackendMovie[]>([]);
  const telegramSeries = ref<BackendSeries[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const fetchTelegramMovies = async () => {
    loading.value = true;
    error.value = null;
    try {
      const res = await fetch(`${backendUrl}/movies`);
      if (!res.ok) {
        throw new Error(`Error: ${res.statusText}`);
      }
      telegramMovies.value = await res.json();
    } catch (e: any) {
      console.error(e);
      error.value = e.message || 'Failed to fetch movies from backend';
    } finally {
      loading.value = false;
    }
  };

  const fetchTelegramSeries = async () => {
    loading.value = true;
    error.value = null;
    try {
      const res = await fetch(`${backendUrl}/series`);
      if (!res.ok) {
        throw new Error(`Error: ${res.statusText}`);
      }
      telegramSeries.value = await res.json();
    } catch (e: any) {
      console.error(e);
      error.value = e.message || 'Failed to fetch series from backend';
    } finally {
      loading.value = false;
    }
  };

  return {
    telegramMovies,
    telegramSeries,
    loading,
    error,
    fetchTelegramMovies,
    fetchTelegramSeries
  };
}
