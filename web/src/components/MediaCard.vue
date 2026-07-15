<template>
  <div class="glass-panel media-card" @mouseenter="isHovered = true" @mouseleave="isHovered = false">
    <div class="media-image-container">
      <img :src="media.coverUrl" :alt="media.title" class="media-image" v-if="media.coverUrl" />
      
      <!-- Circle delete button in top-left, only visible on hover -->
      <button 
        v-if="media.isOnTelegram" 
        class="circle-btn delete-btn" 
        :class="{ visible: isHovered }"
        @click.stop="emit('delete')" 
        title="Borrar respaldo"
      >
        <Trash2 :size="14" />
      </button>

      <!-- Bottom action bar sliding up -->
      <div class="media-action-bar" :class="{ active: isHovered }">
        <button v-if="!media.isOnTelegram" class="action-bar-btn primary" @click.stop="emit('upload', media)">
          <UploadCloud :size="14" />
          <span>Respaldar en Telegram</span>
        </button>
        <button v-else-if="!media.isInJellyfin" class="action-bar-btn secondary" @click.stop="emit('download-all', media)">
          <DownloadCloud :size="14" />
          <span>Descargar Todo</span>
        </button>
        <button v-else class="action-bar-btn success" disabled>
          <CheckCircle :size="14" />
          <span>Sincronizado</span>
        </button>
      </div>

      <div class="media-badges">
        <span class="badge quality" v-if="media.resolutions && media.resolutions.length">{{ media.resolutions[0] }}</span>
        <span class="badge type">{{ media.type }}</span>
        <span class="badge telegram" v-if="media.isOnTelegram">
          <SendIcon :size="12" class="tg-icon" />
          Telegram
        </span>
      </div>
    </div>
    
    <div class="media-content">
      <div class="media-header">
        <h3 class="media-title">{{ media.title || 'Unknown Title' }}</h3>
        <span class="media-year">{{ media.year || '' }}</span>
      </div>
      
      <p v-if="media.synopsis" class="media-synopsis">{{ media.synopsis }}</p>

      <!-- Expandable TV episodes list -->
      <div v-if="media.type === 'series' && media.seasons && media.seasons.length" class="episodes-section">
        <button class="expand-episodes-btn" @click.stop="toggleEpisodes">
          {{ showEpisodes ? 'Ocultar Episodios' : `Ver Episodios (${totalEpisodesCount} respaldados)` }}
        </button>
        <div v-if="showEpisodes" class="episodes-list">
          <div v-for="season in sortedSeasons" :key="season.id" class="season-block">
            <div class="season-header">
              <h5 class="season-title">Temporada {{ season.season_number }}</h5>
              <button 
                v-if="media.isOnTelegram"
                class="season-download-btn" 
                @click.stop="emit('download-season', { seriesId: media.rawId, seasonNumber: season.season_number, title: media.title })"
                title="Descargar temporada"
              >
                <DownloadCloud :size="11" />
                <span>Descargar Temp.</span>
              </button>
            </div>
            <div class="episodes-grid">
              <div v-for="ep in season.episodes" :key="ep.id" class="episode-item">
                <span class="episode-badge">E{{ ep.episode_number }}</span>
                <button 
                  v-if="media.isOnTelegram"
                  class="ep-download-btn" 
                  @click.stop="emit('download-episode', { seriesId: media.rawId, seasonNumber: season.season_number, episodeNumber: ep.episode_number, title: media.title })"
                  title="Descargar capítulo"
                >
                  <DownloadCloud :size="10" />
                </button>
              </div>
              <!-- Packs de temporada -->
              <div v-if="season.collections && season.collections.length" class="episode-item season-pack">
                <span class="episode-badge pack-badge">Pack Completo</span>
                <button 
                  v-if="media.isOnTelegram"
                  class="ep-download-btn" 
                  @click.stop="emit('download-season', { seriesId: media.rawId, seasonNumber: season.season_number, title: media.title })"
                  title="Descargar pack"
                >
                  <DownloadCloud :size="10" />
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { UploadCloud, DownloadCloud, CheckCircle, Send as SendIcon, Trash2 } from 'lucide-vue-next';
import type { Media } from '../api/jellyfin';
import type { BackendSeason } from '../api/backend';

// Extend the Media type with status attributes for UI
interface UIMedia extends Media {
  isOnTelegram?: boolean;
  isInJellyfin?: boolean;
  seasons?: BackendSeason[];
  rawId?: number;
}

const props = defineProps<{
  media: UIMedia
}>();

const emit = defineEmits(['delete', 'download-all', 'download-season', 'download-episode', 'upload']);
const isHovered = ref(false);

const showEpisodes = ref(false);
const toggleEpisodes = () => {
  showEpisodes.value = !showEpisodes.value;
};

const totalEpisodesCount = computed(() => {
  if (!props.media.seasons) return 0;
  let count = 0;
  props.media.seasons.forEach(s => {
    count += s.episodes.length;
    if (s.collections && s.collections.length) {
      count += 1;
    }
  });
  return count;
});

const sortedSeasons = computed(() => {
  if (!props.media.seasons) return [];
  // Clone to avoid mutating props
  const clonedSeasons = JSON.parse(JSON.stringify(props.media.seasons));
  
  return clonedSeasons.sort((a: any, b: any) => a.season_number - b.season_number)
    .map((season: any) => {
      if (season.episodes) {
        season.episodes.sort((a: any, b: any) => a.episode_number - b.episode_number);
      }
      return season;
    });
});
</script>

<style scoped>
.media-card {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
}

.media-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.4);
}

.media-image-container {
  position: relative;
  width: 100%;
  padding-top: 150%; /* 2:3 aspect ratio */
  overflow: hidden;
  background: #1a1c23;
}

.media-image {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform 0.5s ease;
}

.media-card:hover .media-image {
  transform: scale(1.05);
}

.circle-btn {
  position: absolute;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: rgba(15, 17, 21, 0.6);
  backdrop-filter: blur(8px);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.3s ease;
  z-index: 10;
  opacity: 0;
  transform: scale(0.8);
}

.circle-btn.visible {
  opacity: 1;
  transform: scale(1);
}

.circle-btn.delete-btn {
  top: 12px;
  left: 12px;
}

.circle-btn.delete-btn:hover {
  background: rgba(239, 68, 68, 0.85);
  border-color: rgba(239, 68, 68, 0.5);
  box-shadow: 0 4px 12px rgba(239, 68, 68, 0.3);
}

.media-action-bar {
  position: absolute;
  bottom: 0;
  left: 0;
  width: 100%;
  height: 40px;
  transform: translateY(100%);
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  z-index: 9;
}

.media-action-bar.active {
  transform: translateY(0);
}

.action-bar-btn {
  width: 100%;
  height: 100%;
  border: none;
  font-size: 0.85rem;
  font-weight: 600;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  cursor: pointer;
  backdrop-filter: blur(12px);
  transition: background 0.2s ease;
}

.action-bar-btn.primary {
  background: rgba(0, 164, 220, 0.85);
}

.action-bar-btn.primary:hover {
  background: rgba(0, 164, 220, 0.95);
}

.action-bar-btn.secondary {
  background: rgba(170, 92, 195, 0.85);
}

.action-bar-btn.secondary:hover {
  background: rgba(170, 92, 195, 0.95);
}

.action-bar-btn.success {
  background: rgba(16, 185, 129, 0.8);
  cursor: not-allowed;
}

.media-badges {
  position: absolute;
  top: 12px;
  right: 12px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  align-items: flex-end;
}

.badge {
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.5px;
  backdrop-filter: blur(8px);
}

.badge.quality {
  background: rgba(0, 164, 220, 0.85);
  color: white;
}

.badge.type {
  background: rgba(170, 92, 195, 0.85);
  color: white;
}

.badge.telegram {
  background: rgba(0, 136, 204, 0.85);
  color: white;
  display: flex;
  align-items: center;
  gap: 4px;
}

.tg-icon {
  margin-top: -1px;
}

.glass-button.success {
  background: rgba(16, 185, 129, 0.2) !important;
  border: 1px solid rgba(16, 185, 129, 0.4) !important;
  color: #34d399 !important;
  cursor: not-allowed;
}

.glass-button.danger {
  background: rgba(239, 68, 68, 0.2) !important;
  border: 1px solid rgba(239, 68, 68, 0.4) !important;
  color: #f87171 !important;
}

.glass-button.danger:hover {
  background: rgba(239, 68, 68, 0.35) !important;
}

.media-content {
  padding: 16px;
  display: flex;
  flex-direction: column;
  flex-grow: 1;
}

.media-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 8px;
  gap: 8px;
}

.media-title {
  font-size: 1.1rem;
  font-weight: 600;
  line-height: 1.3;
  margin: 0;
  color: var(--text-primary);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.media-year {
  font-size: 0.9rem;
  color: var(--text-secondary);
  font-weight: 500;
  background: rgba(255, 255, 255, 0.1);
  padding: 2px 6px;
  border-radius: 4px;
}

.media-synopsis {
  font-size: 0.85rem;
  color: var(--text-secondary);
  line-height: 1.5;
  margin-bottom: 12px;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
  flex-grow: 1;
}

.media-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: auto;
}

.tag {
  font-size: 0.75rem;
  padding: 2px 8px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: var(--text-secondary);
}

.episodes-section {
  margin-top: auto;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  padding-top: 8px;
}

.expand-episodes-btn {
  background: transparent;
  border: none;
  color: #00a4dc;
  font-size: 0.8rem;
  font-weight: 600;
  cursor: pointer;
  padding: 0;
  display: flex;
  align-items: center;
  gap: 4px;
}

.expand-episodes-btn:hover {
  text-decoration: underline;
}

.episodes-list {
  margin-top: 8px;
  max-height: 120px;
  overflow-y: auto;
  padding-right: 4px;
}

/* Custom scrollbar for episodes list */
.episodes-list::-webkit-scrollbar {
  width: 4px;
}
.episodes-list::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.02);
}
.episodes-list::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 2px;
}

.season-block {
  margin-bottom: 12px;
}

.season-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}

.season-title {
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--text-secondary);
  margin: 0;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.season-download-btn {
  background: rgba(0, 164, 220, 0.1);
  border: 1px solid rgba(0, 164, 220, 0.25);
  border-radius: 4px;
  color: #00a4dc;
  font-size: 0.65rem;
  font-weight: 600;
  padding: 2px 6px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 4px;
  transition: all 0.2s ease;
}

.season-download-btn:hover {
  background: rgba(0, 164, 220, 0.25);
  border-color: rgba(0, 164, 220, 0.4);
}

.episodes-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.episode-item {
  display: flex;
  align-items: center;
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 4px;
  overflow: hidden;
  transition: border-color 0.2s;
}

.episode-item:hover {
  border-color: rgba(255, 255, 255, 0.15);
}

.episode-badge {
  font-size: 0.7rem;
  padding: 3px 6px;
  color: var(--text-primary);
  font-weight: 500;
}

.episode-item.season-pack {
  background: rgba(170, 92, 195, 0.1);
  border-color: rgba(170, 92, 195, 0.25);
}

.episode-item.season-pack .pack-badge {
  color: #d8b4fe;
  font-weight: 600;
}

.ep-download-btn {
  background: rgba(255, 255, 255, 0.06);
  border: none;
  border-left: 1px solid rgba(255, 255, 255, 0.08);
  color: var(--text-secondary);
  padding: 3px 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s ease;
  height: 100%;
}

.ep-download-btn:hover {
  background: rgba(0, 164, 220, 0.2);
  color: #00a4dc;
}

@media (max-width: 768px) {
  .media-content {
    padding: 12px;
  }
  
  .media-header {
    flex-direction: column;
    gap: 4px;
  }
  
  .media-title {
    font-size: 0.9rem;
  }
  
  .media-year {
    font-size: 0.75rem;
    padding: 1px 4px;
  }
  
  .media-synopsis {
    display: none;
  }
  
  /* On mobile, show the bottom action bar by default */
  .media-action-bar {
    transform: translateY(0);
    background: linear-gradient(to top, rgba(15, 17, 21, 0.95) 0%, rgba(15, 17, 21, 0.7) 100%);
  }
  
  .circle-btn {
    opacity: 0.95;
    transform: scale(0.9);
  }
}
</style>
