<template>
  <div class="item-hero glass-panel">
    <div
      v-if="item.poster_path"
      class="hero-backdrop"
      :style="{ backgroundImage: `url(${item.poster_path.startsWith('/') ? 'https://image.tmdb.org/t/p/w780' + item.poster_path : item.poster_path})` }"
    ></div>
    <img v-if="item.poster_path" :src="item.poster_path.startsWith('/') ? 'https://image.tmdb.org/t/p/w500' + item.poster_path : item.poster_path" class="hero-poster" />
    <div class="hero-info">
      <div style="display: flex; align-items: center; gap: 1rem; flex-wrap: wrap;">
        <h1 style="margin: 0; font-size: 2.2rem; font-weight: 700;">{{ title || 'Unknown Title' }}</h1>
        <span class="year-badge" style="background: rgba(255,255,255,0.1); padding: 0.25rem 0.6rem; border-radius: 8px; font-size: 0.9rem; font-weight: 600; border: 1px solid rgba(255,255,255,0.15);">{{ item.release_year }}</span>
        <div v-if="item.tmdb_id" style="display: inline-flex; align-items: center; gap: 0.35rem;">
          <a :href="type === 'movies' ? 'https://www.themoviedb.org/movie/' + item.tmdb_id : 'https://www.themoviedb.org/tv/' + item.tmdb_id" target="_blank" rel="noopener noreferrer" class="tmdb-link" style="display: inline-flex; align-items: center; gap: 0.4rem; background: rgba(13, 37, 63, 0.65); border: 1px solid rgba(1, 180, 228, 0.4); padding: 0.25rem 0.6rem; border-radius: 8px; font-size: 0.85rem; font-weight: 600; color: #90cea1; text-decoration: none; transition: all 0.2s; height: 30px;">
            <span style="color: #01b4e4; font-weight: 800;">TMDB:</span> {{ item.tmdb_id }}
          </a>
          <button @click="copyTmdbId" class="glass-button" style="padding: 0; border-radius: 8px; display: inline-flex; align-items: center; justify-content: center; height: 30px; width: 30px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.1); cursor: pointer;" title="Copy TMDB ID">
            <Check v-if="copied" :size="13" style="color: #4ade80;" />
            <Copy v-else :size="13" style="color: #a1a1aa;" />
          </button>
        </div>
      </div>
      <p class="overview" style="margin-top: 1rem; color: #d1d5db; line-height: 1.6;">{{ item.overview }}</p>
      <div class="admin-actions" style="margin-top: 1.5rem; display: flex; gap: 0.75rem; flex-wrap: wrap;">
        <router-link v-if="jellyfinItemId" :to="'/jellyfin/' + type + '/' + jellyfinItemId" class="glass-button" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff; text-decoration: none; display: inline-flex; align-items: center; gap: 0.5rem; padding: 6px 12px; font-size: 0.9rem; border-radius: 8px;">
          📂 View Jellyfin Card
        </router-link>
        <button @click="emit('reidentify')" class="glass-button" style="background: rgba(214, 186, 255, 0.14); border-color: rgba(214, 186, 255, 0.30); color: #d6baff;">
          🔍 Re-identify (TMDB ID)
        </button>
        <button v-if="type === 'series'" @click="emit('match-jellyfin')" class="glass-button" :disabled="matchingWithJellyfin" style="background: rgba(96, 165, 250, 0.14); border-color: rgba(96, 165, 250, 0.30); color: #60a5fa;" title="Match every episode against the video files already on disk and read their technical data">
          <FolderSync :size="16" /> {{ matchingWithJellyfin ? 'Matching…' : 'Match with Jellyfin' }}
        </button>
        <button @click="emit('change-poster')" class="glass-button" style="background: rgba(34, 197, 94, 0.14); border-color: rgba(34, 197, 94, 0.30); color: #7ee2a8;">
          🖼️ Change Poster
        </button>
        <button @click="emit('delete')" class="glass-button danger" style="background: rgba(255, 180, 171, 0.12); border-color: rgba(255, 180, 171, 0.30); color: #ffb4ab;">
          🗑️ Delete All
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Copy, Check, FolderSync } from 'lucide-vue-next'
import { copyText } from '../../utils/clipboard'
import type { CatalogItem } from '../../api/cinegram'

const props = defineProps<{
  item: CatalogItem
  type: string
  jellyfinItemId: string | null
  matchingWithJellyfin: boolean
}>()

const emit = defineEmits<{
  reidentify: []
  'match-jellyfin': []
  'change-poster': []
  delete: []
}>()

const title = computed(() => 'title' in props.item ? props.item.title : props.item.manual_title)

const copied = ref(false)

const copyTmdbId = () => {
  if (!props.item.tmdb_id) return
  if (!copyText(props.item.tmdb_id.toString())) {
    alert("Could not copy. Please copy it manually.")
    return
  }
  copied.value = true
  setTimeout(() => {
    copied.value = false
  }, 2000)
}
</script>

<style scoped src="./buttons.css"></style>
<style scoped>
.hero-poster {
  position: relative;
  z-index: 1;
  width: 240px;
  border-radius: var(--r-xl);
  box-shadow: 0 20px 40px rgba(0,0,0,0.6);
  border: 1px solid var(--glass-border);
}
.item-hero {
  position: relative;
  display: flex;
  gap: 2rem;
  margin-bottom: 2rem;
  padding: 1.5rem;
  border-radius: var(--r-2xl);
  overflow: hidden;
}
.hero-backdrop {
  position: absolute;
  inset: 0;
  background-size: cover;
  background-position: center;
  filter: blur(24px) brightness(0.4);
  transform: scale(1.1);
  z-index: 0;
}
.item-hero > .hero-info,
.item-hero > .hero-poster {
  position: relative;
  z-index: 1;
}
.hero-info {
  position: relative;
  z-index: 1;
}
.hero-info h1 {
  margin-top: 0;
  font-size: 2.5rem;
}
.overview {
  line-height: 1.6;
  opacity: 0.8;
}
.tmdb-link:hover {
  background: rgba(13, 37, 63, 0.9) !important;
  border-color: rgba(1, 180, 228, 0.8) !important;
  box-shadow: 0 0 10px rgba(1, 180, 228, 0.3);
  transform: translateY(-1px);
}

@media (max-width: 768px) {
  .item-hero {
    flex-direction: column;
  }
  .hero-poster {
    width: 100%;
    max-width: 200px;
    margin: 0 auto;
  }
}
</style>
