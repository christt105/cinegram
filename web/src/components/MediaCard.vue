<template>
  <article class="media-card glass-panel" @click="goToDetail" @mouseenter="isHovered = true" @mouseleave="isHovered = false">
    <div class="media-image-container">
      <img :src="media.coverUrl" :alt="media.title" class="media-image" v-if="media.coverUrl" />
      <div v-else class="media-image-fallback">
        <Clapperboard :size="40" />
      </div>

      <!-- Collections count (Telegram library) or backup status badge -->
      <div v-if="showCollectionCount" class="status-badge collections">
        <Layers :size="12" />
        {{ media.collectionCount || 0 }} {{ (media.collectionCount || 0) === 1 ? 'collection' : 'collections' }}
      </div>
      <div v-else class="status-badge" :class="media.isOnTelegram ? 'backed-up' : 'pending'">
        <component :is="media.isOnTelegram ? CheckCircle : RefreshCw" :size="12" />
        {{ media.isOnTelegram ? 'Backed up' : 'Pending' }}
      </div>

      <!-- Type chip -->
      <span class="type-chip label-caps">{{ media.type }}</span>

      <!-- Hover overlay -->
      <div class="media-overlay" :class="{ active: isHovered }">
        <span class="overlay-btn label-caps">
          <Info :size="14" /> View Details
        </span>
      </div>
    </div>

    <div class="media-content">
      <h3 class="media-title">{{ media.title || 'Unknown Title' }}</h3>
      <div class="media-meta">
        <span class="media-year">{{ media.year || '—' }}</span>
        <span class="media-quality label-caps" v-if="media.resolutions && media.resolutions.length">
          {{ media.resolutions[0] }}
        </span>
      </div>
    </div>
  </article>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { CheckCircle, RefreshCw, Info, Clapperboard, Layers } from 'lucide-vue-next';
import type { Media } from '../api/jellyfin';
import type { BackendSeason } from '../api/backend';

interface UIMedia extends Media {
  isOnTelegram?: boolean;
  isInJellyfin?: boolean;
  seasons?: BackendSeason[];
  rawId?: number;
  collectionCount?: number;
}

const props = withDefaults(defineProps<{
  media: UIMedia,
  showCollectionCount?: boolean
}>(), {
  showCollectionCount: false
});

defineEmits(['delete', 'download-all', 'download-season', 'download-episode', 'upload']);
const isHovered = ref(false);
const router = useRouter();

const goToDetail = () => {
  const typeParam = props.media.type === 'movie' ? 'movies' : 'series';
  if (props.media.id.startsWith('tg-')) {
    router.push(`/item/${typeParam}/${props.media.rawId}`);
  } else {
    router.push(`/jellyfin/${typeParam}/${props.media.id}`);
  }
};
</script>

<style scoped>
.media-card {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
  cursor: pointer;
  border-radius: var(--r-xl);
}

.media-card:hover {
  transform: translateY(-4px) scale(1.02);
  box-shadow: var(--glass-shadow);
}

.media-image-container {
  position: relative;
  width: 100%;
  aspect-ratio: 2 / 3;
  overflow: hidden;
  background: var(--surface-container-highest);
}

.media-image {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform 0.7s ease;
}

.media-card:hover .media-image {
  transform: scale(1.1);
}

.media-image-fallback {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--outline);
}

/* Status badge */
.status-badge {
  position: absolute;
  top: 8px;
  right: 8px;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 7px;
  border-radius: var(--r-sm);
  font-family: 'Geist', 'Inter', sans-serif;
  font-size: 10px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  backdrop-filter: blur(8px);
}

.status-badge.backed-up {
  background: rgba(34, 197, 94, 0.9);
  color: #05270f;
}

.status-badge.pending {
  background: rgba(53, 53, 52, 0.8);
  color: var(--on-surface-variant);
  border: 1px solid var(--outline-variant);
}

.status-badge.collections {
  background: rgba(14, 14, 14, 0.72);
  color: var(--secondary);
  border: 1px solid var(--outline-variant);
}

.type-chip {
  position: absolute;
  top: 8px;
  left: 8px;
  padding: 3px 7px;
  border-radius: var(--r-sm);
  font-size: 10px;
  background: rgba(14, 14, 14, 0.65);
  color: var(--secondary);
  backdrop-filter: blur(8px);
}

/* Hover overlay */
.media-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: flex-end;
  justify-content: center;
  padding: 16px;
  background: linear-gradient(to top, rgba(0, 0, 0, 0.85), transparent 55%);
  opacity: 0;
  transition: opacity 0.3s ease;
}

.media-overlay.active {
  opacity: 1;
}

.overlay-btn {
  width: 100%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 8px;
  border-radius: var(--r-lg);
  background: var(--primary);
  color: var(--on-primary);
}

.media-content {
  padding: 14px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  background: var(--glass-bg);
}

.media-title {
  font-size: 1rem;
  font-weight: 600;
  line-height: 1.3;
  margin: 0;
  color: var(--on-surface);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.media-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.media-year {
  font-size: 0.85rem;
  color: var(--on-surface-variant);
}

.media-quality {
  color: var(--tertiary);
  font-size: 11px;
}

@media (max-width: 768px) {
  .media-content {
    padding: 12px 14px;
  }

  .media-title {
    font-size: 0.85rem;
    white-space: normal;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
  }

  .media-overlay {
    display: none;
  }
}
</style>
