import { ref } from 'vue';
import { apiUrl } from '../config';

export interface TelegramFile {
  id: number;
  message_id: number;
  filename: string;
  filesize: number;
  mime_type: string | null;
  created_at: string;
  collection_id: number;
}

export interface TelegramCollection {
  id: number;
  name: string | null;
  movie_id: number | null;
  episode_id: number | null;
  season_id: number | null;
  files: TelegramFile[];
  quality: string | null;
  codec: string | null;
  audio_languages: string | null;
  subtitle_languages: string | null;
  tags: string | null;
  notes: string | null;
  technical_metadata: string | null;
  local_path: string | null;
}

export interface CatalogMovie {
  id: number;
  tmdb_id: number | null;
  title: string | null;
  poster_path: string | null;
  collections: TelegramCollection[];
  release_year: number | null;
  overview: string | null;
  tags: string | null;
  notes: string | null;
  created_at: string;
}

export interface CatalogEpisode {
  id: number;
  episode_number: number;
  title: string | null;
  collections: TelegramCollection[];
}

export interface CatalogSeason {
  id: number;
  season_number: number;
  local_metadata: boolean;
  episodes: CatalogEpisode[];
  collections: TelegramCollection[];
}

export interface CatalogSeries {
  id: number;
  tmdb_id: number | null;
  tvdb_id: number | null;
  manual_title: string | null;
  poster_path: string | null;
  overview: string | null;
  release_year: number | null;
  seasons: CatalogSeason[];
  created_at: string;
}

export type CatalogItem = CatalogMovie | CatalogSeries;

export interface SeriesMatchPlan {
  matches: { collection_id: number; path: string; season: number; episode: number }[];
  skips: { season: number; episode: number; reason: string }[];
}

export type WatchedFileStatus =
  | 'pending'
  | 'notified'
  | 'confirmed'
  | 'corrected'
  | 'moved'
  | 'removed'
  | 'error';

export interface WatchedFile {
  id: number;
  path: string;
  filename: string;
  filesize: number;
  first_seen_at: string;
  guess_media_type?: 'movie' | 'tv' | null;
  guess_tmdb_id?: number | null;
  guess_title?: string | null;
  guess_year?: number | null;
  guess_season?: number | null;
  guess_episode?: number | null;
  confidence: number;
  guess_source?: string | null;
  status: WatchedFileStatus;
  notified_at?: string | null;
  moved_path?: string | null;
  error_message?: string | null;
}

export interface WatchedFileResolution {
  id: number;
  path: string;
  filename: string;
  tmdb_id: number;
  media_type: string;
  title: string;
  year?: number | null;
  season?: number | null;
  episode?: number | null;
  status: string;
}

export async function listWatchedFiles(status?: WatchedFileStatus): Promise<WatchedFile[]> {
  const url = status ? `${apiUrl}/watch?status=${status}` : `${apiUrl}/watch`;
  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(`Failed to list watched files: ${res.statusText}`);
  }
  return res.json();
}

export async function confirmWatchedFile(
  id: number,
  tmdbId: number,
  season?: number | null,
  episode?: number | null
): Promise<WatchedFileResolution> {
  const res = await fetch(`${apiUrl}/watch/${id}/confirm`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ tmdb_id: tmdbId, season, episode })
  });
  if (!res.ok) {
    throw new Error(`Failed to confirm watched file ${id}: ${res.statusText}`);
  }
  return res.json();
}

export interface MetadataVocabulary {
  qualities: string[];
  codecs: string[];
  languages: Record<string, string>;
}

export async function fetchMetadataVocabulary(): Promise<MetadataVocabulary> {
  const res = await fetch(`${apiUrl}/collections/vocabulary`);
  if (!res.ok) {
    throw new Error(`Failed to fetch metadata vocabulary: ${res.statusText}`);
  }
  return res.json();
}

export async function reidentifyWatchedFiles(): Promise<WatchedFile[]> {
  const res = await fetch(`${apiUrl}/watch/reidentify`, { method: 'POST' });
  if (!res.ok) {
    throw new Error(`Failed to re-identify watched files: ${res.statusText}`);
  }
  return res.json();
}

export async function correctWatchedFile(
  id: number,
  tmdbId: number,
  season?: number | null,
  episode?: number | null
): Promise<WatchedFileResolution> {
  const res = await fetch(`${apiUrl}/watch/${id}/correct`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ tmdb_id: tmdbId, season, episode })
  });
  if (!res.ok) {
    throw new Error(`Failed to correct watched file ${id}: ${res.statusText}`);
  }
  return res.json();
}

export function useCatalog() {
  const telegramMovies = ref<CatalogMovie[]>([]);
  const telegramSeries = ref<CatalogSeries[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const fetchTelegramMovies = async () => {
    loading.value = true;
    error.value = null;
    try {
      const res = await fetch(`${apiUrl}/movies`);
      if (!res.ok) {
        throw new Error(`Error: ${res.statusText}`);
      }
      telegramMovies.value = await res.json();
    } catch (e: any) {
      console.error(e);
      error.value = e.message || 'Failed to fetch movies from Cinegram';
    } finally {
      loading.value = false;
    }
  };

  const fetchTelegramSeries = async () => {
    loading.value = true;
    error.value = null;
    try {
      const res = await fetch(`${apiUrl}/series`);
      if (!res.ok) {
        throw new Error(`Error: ${res.statusText}`);
      }
      telegramSeries.value = await res.json();
    } catch (e: any) {
      console.error(e);
      error.value = e.message || 'Failed to fetch series from Cinegram';
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

export type HealthVerdict = 'ok' | 'starting' | 'disabled' | 'unhealthy';
export type BotState = 'starting' | 'ready' | 'failed' | 'disabled';
export type AccountSession = 'not_checked' | 'restored' | 'missing' | 'failed';
export type LoopState = 'waiting' | 'running' | 'done' | 'skipped' | 'stopped' | 'crashed';

export interface HealthReport {
  status: HealthVerdict;
  healthy: boolean;
  problems: string[];
  warnings: string[];
  bot: { state: BotState; problem: string | null; since: string; last_answer: string | null; answering: boolean | null };
  account: { session: AccountSession; premium: boolean; problem: string | null } | null;
  loops: { name: string; state: LoopState; problem: string | null; since: string }[];
}

/** Answers 503 with the same report when something is wrong, so only a missing body is an error. */
export async function fetchHealth(): Promise<HealthReport> {
  const res = await fetch(`${apiUrl}/health`);
  if (!res.ok && res.status !== 503) throw new Error(`Health check failed (${res.status})`);
  return res.json();
}
