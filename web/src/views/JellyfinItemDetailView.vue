<template>
  <div class="item-detail-page">
    <div class="header-nav">
      <button @click="$router.back()" class="glass-button">
        &larr; Back
      </button>
      <div style="flex-grow:1"></div>
    </div>

    <div v-if="isLoading" class="loading-state">Loading...</div>
    <div v-else-if="item" class="item-content">
      
      <div class="item-hero glass-panel">
        <div
          v-if="item.poster_path"
          class="hero-backdrop"
          :style="{ backgroundImage: `url(${item.poster_path})` }"
        ></div>
        <img v-if="item.poster_path" :src="item.poster_path" class="hero-poster" />
        <div class="hero-info">
          <div style="display: flex; align-items: center; gap: 1rem; flex-wrap: wrap;">
            <h1 style="margin: 0; font-size: 2.2rem; font-weight: 700;">{{ item.Name || 'Unknown Title' }}</h1>
            <span class="year-badge" style="background: rgba(255,255,255,0.1); padding: 0.25rem 0.6rem; border-radius: 8px; font-size: 0.9rem; font-weight: 600; border: 1px solid rgba(255,255,255,0.15);">{{ item.ProductionYear }}</span>
          </div>
          <p class="overview" style="margin-top: 1rem; color: #d1d5db; line-height: 1.6;">{{ item.Overview }}</p>
          
          <div class="actions-row" style="margin-top: 1.5rem; display: flex; gap: 0.75rem; flex-wrap: wrap; align-items: center;">
            <router-link v-if="localDbItem" :to="'/item/' + type + '/' + localDbItem.id" class="glass-button" style="background: rgba(34, 197, 94, 0.14); border-color: rgba(34, 197, 94, 0.30); color: #7ee2a8; text-decoration: none; display: inline-flex; align-items: center; gap: 0.5rem; padding: 6px 12px; font-size: 0.9rem; border-radius: 8px;">
              📂 View Telegram Card
            </router-link>
            
            <div class="upload-action" v-if="type === 'movies' && !(item.MediaSources && item.MediaSources.length > 1)">
              <button @click="uploadItem(item, 'movie')" class="glass-button primary">
                <UploadCloud :size="16" /> Upload to Telegram
              </button>
            </div>
          </div>

          <div class="versions-block" style="margin-top: 1.5rem;" v-if="type === 'movies' && item.MediaSources && item.MediaSources.length > 1">
            <h3 style="font-size: 1.1rem; color: var(--on-surface-variant); margin: 0 0 0.5rem 0;">Available versions:</h3>
            <div style="display:flex; flex-direction:column; gap:0.5rem;">
              <div v-for="ms in item.MediaSources" :key="ms.Id" class="version-row">
                <div class="version-info">
                  <span class="version-name">{{ ms.Name || 'Alternative version' }}</span>
                  <span class="version-detail">{{ getMediaSourceDetails(ms) }}</span>
                </div>
                <button @click="uploadItem(item, 'movie', ms.Path)" class="glass-button primary btn-sm version-upload">
                  <UploadCloud :size="14" /> Upload
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="item-actions">
        <!-- Series have seasons and episodes -->
        <div v-if="type === 'series'" class="seasons-section">
          <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom: 1.5rem;">
            <h2 style="font-size: 2rem; margin:0;">Seasons</h2>
            <button @click="uploadItem(item, 'series')" class="glass-button primary">
               <UploadCloud :size="16" /> Upload Full Series
            </button>
          </div>
          
          <div v-for="season in item.seasons" :key="season.Id" class="season-block">
            
            <div class="season-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid rgba(255,255,255,0.1); flex-wrap: wrap; gap: 1rem;">
              <h3 style="font-size: 1.5rem; margin: 0;">{{ season.Name }}</h3>
              <button @click="uploadItem(season, 'series')" class="glass-button primary">
                <UploadCloud :size="16" /> Upload Season
              </button>
            </div>
            
            <div class="episode-list">
              <div v-for="ep in season.episodes" :key="ep.Id" class="episode-item glass-panel" style="flex-direction: row; align-items: center; justify-content: space-between; gap: 1rem; padding: 1rem;">
                <div class="ep-info" style="display: flex; flex-direction: column; gap: 0.25rem;">
                  <strong style="color: var(--jellyfin-blue);">E{{ ep.IndexNumber }}</strong>
                  <span style="font-weight: 500;">{{ ep.Name }}</span>
                </div>
                
                <div class="ep-actions" style="display:flex; flex-direction:column; gap:0.5rem;">
                  <div v-if="ep.MediaSources && ep.MediaSources.length > 1" style="display:flex; flex-direction:column; gap:0.5rem; align-items:flex-end;">
                    <div v-for="ms in ep.MediaSources" :key="ms.Id" style="display:flex; gap:0.5rem; align-items:center;">
                      <div style="display: flex; flex-direction: column; align-items: flex-end;">
                        <span style="font-size:0.75rem; font-weight: bold; color:#e4e4e7; max-width: 150px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;" :title="ms.Name">{{ ms.Name || 'Version' }}</span>
                        <span style="font-size:0.65rem; color:#a1a1aa; max-width: 150px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;" :title="getMediaSourceDetails(ms)">{{ getMediaSourceDetails(ms) }}</span>
                      </div>
                      <button @click="uploadItem(ep, 'series', ms.Path)" class="glass-button primary btn-sm icon-only" title="Upload Version">
                        <UploadCloud :size="14" />
                      </button>
                    </div>
                  </div>
                  <div v-else>
                    <button @click="uploadItem(ep, 'series')" class="glass-button primary btn-sm icon-only" title="Upload Episode">
                      <UploadCloud :size="16" />
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
        
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { UploadCloud } from 'lucide-vue-next'
import { fetchItemDetails, getImageUrl } from '../api/jellyfin'

const props = defineProps<{
  type: string
  id: string
}>()

const item = ref<any>(null)
const isLoading = ref(true)

const backendUrl = import.meta.env.VITE_BACKEND_URL || `${window.location.protocol}//${window.location.hostname}:${import.meta.env.VITE_BACKEND_PORT || 8005}`

const fetchItem = async () => {
  isLoading.value = true
  try {
    const data = await fetchItemDetails(props.id, props.type as 'movie' | 'series')
    if (data.ImageTags && data.ImageTags.Primary) {
      data.poster_path = getImageUrl(data.Id, data.ImageTags.Primary, 500)
    }
    
    if (data.MediaSources) {
        // filter out 'Grouping' type if 'Default' with same Path exists, to avoid duplicates
        const uniquePaths = new Set();
        data.MediaSources = data.MediaSources.filter((ms: any) => {
            if (uniquePaths.has(ms.Path)) return false;
            uniquePaths.add(ms.Path);
            return true;
        });
    }
    
    // sorting seasons and episodes just in case
    if (data.seasons) {
        data.seasons.sort((a: any, b: any) => (a.IndexNumber || 0) - (b.IndexNumber || 0));
        data.seasons.forEach((s: any) => {
            if (s.episodes) {
                s.episodes.sort((a: any, b: any) => (a.IndexNumber || 0) - (b.IndexNumber || 0));
            }
        });
    }
    
    item.value = data
    if (data.ProviderIds?.Tmdb) {
      checkLocalDatabase(data.ProviderIds.Tmdb)
    }
  } catch (err) {
    console.error(err)
  } finally {
    isLoading.value = false
  }
}

const localDbItem = ref<any>(null)
const checkLocalDatabase = async (tmdbId: string) => {
  if (!tmdbId) return
  try {
    const typePath = props.type === 'movies' ? 'movies' : 'series'
    const res = await fetch(`${backendUrl}/${typePath}/tmdb/${tmdbId}`)
    if (res.ok) {
      const data = await res.json()
      if (data && data.id) {
        localDbItem.value = data
      }
    }
  } catch (err) {
    console.error("Error checking local DB:", err)
  }
}

const uploadItem = async (target: any, mediaType: string, forcePath?: string) => {
    try {
        const payload = {
            jellyfin_id: target.Id,
            tmdb_id: item.value.ProviderIds?.Tmdb ? parseInt(item.value.ProviderIds.Tmdb) : null,
            media_type: mediaType,
            path: forcePath || target.Path || item.value.Path,
            title: target.Name || item.value.Name,
            year: item.value.ProductionYear
        };
        
        const res = await fetch(`${backendUrl}/uploads/enqueue`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        
        if (res.ok) {
            alert("Upload added to Telegram queue.");
        } else {
            alert("Error queuing upload.");
        }
    } catch (e) {
        console.error(e);
        alert("Error queuing upload.");
    }
}

const formatBytes = (bytes: number) => {
    if (!bytes) return '';
    const gb = bytes / (1024 * 1024 * 1024);
    return gb.toFixed(2) + ' GB';
}

const formatBitrate = (bitrate: number) => {
    if (!bitrate) return '';
    const mbps = bitrate / 1000000;
    return mbps.toFixed(1) + ' Mbps';
}

const getMediaSourceDetails = (ms: any) => {
    let res = [];
    if (ms.Size) res.push(formatBytes(ms.Size));
    if (ms.Bitrate) res.push(formatBitrate(ms.Bitrate));

    if (!ms.MediaStreams) return res.join(' | ');
    const video = ms.MediaStreams.find((s:any) => s.Type === 'Video');
    const audios = ms.MediaStreams.filter((s:any) => s.Type === 'Audio' && s.Language).map((s:any) => s.Language);
    const subs = ms.MediaStreams.filter((s:any) => s.Type === 'Subtitle' && s.Language).map((s:any) => s.Language);
    
    if (video) res.push(`${video.DisplayTitle || video.Codec}`);
    if (audios.length > 0) res.push(`Audio: ${[...new Set(audios)].join(', ')}`);
    if (subs.length > 0) res.push(`Subs: ${[...new Set(subs)].join(', ')}`);
    return res.join(' | ');
}

onMounted(() => {
  fetchItem()
})
</script>

<style scoped>
.item-detail-page {
  display: flex;
  flex-direction: column;
  gap: 2rem;
  padding: 1rem 0;
  overflow-wrap: anywhere;
  word-break: break-word;
}

.header-nav {
  display: flex;
  align-items: center;
}

.item-hero {
  position: relative;
  display: flex;
  gap: 2rem;
  margin-bottom: 2rem;
  align-items: flex-start;
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

.hero-poster {
  width: 240px;
  border-radius: var(--r-xl);
  box-shadow: 0 20px 40px rgba(0,0,0,0.6);
  border: 1px solid var(--glass-border);
  object-fit: cover;
}

.hero-info h1 {
  font-size: 2.5rem;
  margin-bottom: 0.5rem;
  color: var(--text-primary);
}

.year {
  color: var(--jellyfin-blue);
  font-size: 1.2rem;
  font-weight: 600;
  margin-bottom: 1rem;
}

.overview {
  font-size: 1.1rem;
  line-height: 1.6;
  color: var(--text-secondary);
}

.seasons-section {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.season-block {
  display: flex;
  flex-direction: column;
  background: rgba(255, 255, 255, 0.02);
  border-radius: 12px;
  padding: 1.5rem;
}

.episode-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1rem;
}

.version-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
  background: var(--surface-container-lowest);
  border: 1px solid var(--glass-border);
  padding: 0.6rem 1rem;
  border-radius: var(--r-lg);
}

.version-info {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  min-width: 0;
}

.version-name {
  font-size: 0.9rem;
  font-weight: 700;
}

.version-detail {
  font-size: 0.75rem;
  color: var(--on-surface-variant);
  overflow-wrap: anywhere;
}

@media (max-width: 768px) {
  .item-detail-page {
    overflow-x: hidden;
  }
  .item-hero {
    flex-direction: column;
    align-items: stretch;
    text-align: left;
  }

  .hero-poster {
    width: 200px;
    margin: 0 auto;
  }

  /* Full-width, tappable action buttons */
  .actions-row {
    flex-direction: column;
    align-items: stretch;
  }
  .actions-row > .glass-button,
  .actions-row > .upload-action,
  .actions-row > .upload-action > .glass-button {
    width: 100%;
    justify-content: center;
  }

  .version-row {
    flex-direction: column;
    align-items: stretch;
  }
  .version-upload {
    width: 100%;
    justify-content: center;
  }

  .episode-list {
    grid-template-columns: 1fr;
  }

  .episode-item {
    flex-wrap: wrap;
  }
}
</style>
