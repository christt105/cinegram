<template>
  <div class="item-detail-page">
    <div class="header-nav">
      <button @click="$router.back()" class="glass-button">
        &larr; Volver
      </button>
      <div style="flex-grow:1"></div>
    </div>

    <div v-if="isLoading" class="loading-state">Loading...</div>
    <div v-else-if="item" class="item-content">
      
      <div class="item-hero glass-panel" style="padding: 1.5rem; border-radius: 16px;">
        <img v-if="item.poster_path" :src="item.poster_path.startsWith('/') ? 'https://image.tmdb.org/t/p/w500' + item.poster_path : item.poster_path" class="hero-poster" />
        <div class="hero-info">
          <h1>{{ item.title || item.manual_title || 'Unknown Title' }}</h1>
          <p class="year">{{ item.release_year }}</p>
          <p class="overview">{{ item.overview }}</p>
        </div>
      </div>

      <div class="item-actions">
        <!-- Movies have collections directly -->
        <div v-if="type === 'movies'" class="collections-section">
          <h2>Available Versions</h2>
          <div v-if="item.collections && item.collections.length > 0" class="collection-list">
            <div v-for="col in item.collections" :key="col.id" class="collection-item glass-panel">
              <div class="col-info" style="display: flex; flex-direction: column; gap: 0.5rem;">
                <strong style="font-size: 1.1rem; color: #4ade80;">{{ col.name || col.quality || 'Respaldo Completo' }}</strong>
                <span v-if="col.audio_languages" style="font-size: 0.9rem; color: #a1a1aa;">Audio: {{ col.audio_languages }}</span>
                <span v-if="getTechMeta(col)" style="font-size: 0.8rem; color: #a1a1aa; max-width: 100%;">{{ getTechMeta(col) }}</span>
                <span v-else-if="col.technical_metadata" class="meta-badge">Info. Técnica Disponible</span>
              </div>
              <div class="col-actions" style="display: flex; gap: 0.5rem; flex-wrap: wrap; justify-content: flex-end;">
                <button @click="downloadCollection(col.id)" class="glass-button primary">
                  <DownloadCloud :size="16" /> Descargar
                </button>
                <button @click="deleteCollection(col.id)" class="glass-button danger">
                  <Trash2 :size="16" /> Borrar
                </button>
              </div>
            </div>
          </div>
          <div v-else>No versions uploaded to Telegram yet.</div>
        </div>

        <!-- Series have seasons and episodes -->
        <div v-if="type === 'series'" class="seasons-section">
          <h2 style="margin-bottom: 1.5rem; font-size: 2rem;">Temporadas</h2>
          <div v-for="season in item.seasons" :key="season.id" class="season-block">
            
            <div class="season-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid rgba(255,255,255,0.1); flex-wrap: wrap; gap: 1rem;">
              <h3 style="font-size: 1.5rem; margin: 0;">Temporada {{ season.season_number }}</h3>
              <button @click="downloadSeason(season.season_number)" class="glass-button primary">
                <DownloadCloud :size="16" /> Descargar Temp.
              </button>
            </div>
            
            <div class="episode-list">
              <div v-for="ep in season.episodes" :key="ep.id" class="episode-item glass-panel" style="flex-direction: column; align-items: stretch; gap: 0;">
                <div class="ep-info" style="border-bottom: 1px solid rgba(255,255,255,0.1); padding-bottom: 0.75rem; margin-bottom: 0.75rem;">
                  <strong style="color: var(--jellyfin-blue);">E{{ ep.episode_number }}</strong>
                  <span style="font-weight: 500;">{{ ep.title?.replace(/^Episode\s+\d+$/, '') || 'Episodio ' + ep.episode_number }}</span>
                </div>
                
                <div v-if="ep.collections && ep.collections.length > 0" class="ep-collections" style="width: 100%;">
                  <div v-for="col in ep.collections" :key="col.id" class="collection-item" style="background: rgba(0,0,0,0.2); padding: 0.5rem; border-radius: 8px; margin-bottom: 0.5rem;">
                    <div style="display: flex; flex-direction: column;">
                      <span style="font-size: 0.85rem; color: #a1a1aa;">{{ col.quality || 'Auto' }}</span>
                      <span style="font-size: 0.75rem; color: #666;">{{ col.files?.length || 0 }} archivos</span>
                    </div>
                    <div style="display: flex; gap: 0.25rem;">
                      <button @click="downloadCollection(col.id)" class="glass-button primary btn-sm icon-only" title="Descargar">
                        <DownloadCloud :size="14" />
                      </button>
                      <button @click="deleteCollection(col.id)" class="glass-button danger btn-sm icon-only" title="Borrar">
                        <Trash2 :size="14" />
                      </button>
                    </div>
                  </div>
                </div>
                <div v-else class="no-col" style="color: var(--text-secondary); font-size: 0.85rem;">No respaldado aún</div>
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
import { DownloadCloud, Trash2 } from 'lucide-vue-next'

const props = defineProps<{
  type: string
  id: string
}>()

const item = ref<any>(null)
const isLoading = ref(true)

const backendUrl = import.meta.env.VITE_BACKEND_URL || 'http://192.168.1.15:8005'

const fetchItem = async () => {
  isLoading.value = true
  try {
    const endpoint = props.type === 'movies' ? `/movies/${props.id}` : `/series/${props.id}`
    const res = await fetch(`${backendUrl}${endpoint}`)
    if (res.ok) {
      const data = await res.json()
      if (data.seasons) {
        data.seasons.sort((a: any, b: any) => a.season_number - b.season_number)
        data.seasons.forEach((season: any) => {
          if (season.episodes) {
            season.episodes.sort((a: any, b: any) => a.episode_number - b.episode_number)
          }
        })
      }
      item.value = data
    }
  } catch (err) {
    console.error(err)
  } finally {
    isLoading.value = false
  }
}

const downloadCollection = async (collectionId: number) => {
  try {
    const res = await fetch(`${backendUrl}/downloads/enqueue/collection/${collectionId}`, { method: 'POST' })
    if (res.ok) {
      alert("Descarga añadida a la cola.")
    } else {
      alert("Error al añadir descarga.")
    }
  } catch (err) {
    console.error(err)
  }
}

const downloadSeason = async (seasonNumber: number) => {
  try {
    const res = await fetch(`${backendUrl}/downloads/enqueue/series/${props.id}/season/${seasonNumber}`, { method: 'POST' })
    if (res.ok) {
      alert("Temporada completa añadida a la cola.")
    } else {
      alert("Error al descargar temporada.")
    }
  } catch (err) {
    console.error(err)
  }
}

const getTechMeta = (col: any) => {
    if (!col.technical_metadata) return null;
    try {
        const meta = JSON.parse(col.technical_metadata);
        let res = [];
        if (meta.video_codec) res.push(`${meta.video_codec}`);
        if (meta.hdr && meta.hdr !== 'SDR') res.push(`${meta.hdr}`);
        if (meta.audio_languages && meta.audio_languages.length > 0) res.push(`Aud: ${meta.audio_languages.join(',')}`);
        if (meta.subtitle_languages && meta.subtitle_languages.length > 0) res.push(`Sub: ${meta.subtitle_languages.join(',')}`);
        return res.length > 0 ? res.join(' | ') : null;
    } catch (e) {
        return null;
    }
}

const deleteCollection = async (collectionId: number) => {
  if (!confirm("Are you sure you want to delete this collection from the database?")) return;
  try {
    const res = await fetch(`${backendUrl}/collections/${collectionId}`, { method: 'DELETE' })
    if (res.ok) {
      alert("Collection deleted.")
      fetchItem() // refresh
    } else {
      alert("Failed to delete collection.")
    }
  } catch (err) {
    console.error(err)
  }
}

onMounted(() => {
  fetchItem()
})
</script>

<style scoped>
.item-detail-page {
  padding: 2rem;
  max-width: 1000px;
  margin: 0 auto;
}
.header-nav {
  margin-bottom: 2rem;
}
.hero-poster {
  width: 250px;
  border-radius: 12px;
  box-shadow: 0 10px 30px rgba(0,0,0,0.5);
}
.item-hero {
  display: flex;
  gap: 2rem;
  margin-bottom: 2rem;
}
.hero-info h1 {
  margin-top: 0;
  font-size: 2.5rem;
}
.hero-info .year {
  color: #4ade80;
  font-weight: bold;
  font-size: 1.2rem;
}
.overview {
  line-height: 1.6;
  opacity: 0.8;
}
.collection-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
.episode-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 0.75rem;
}
.episode-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  flex-wrap: wrap;
  gap: 0.5rem;
}
.ep-info {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  flex: 1 1 auto;
}
.season-block {
  margin-bottom: 2.5rem;
}
.collection-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.2rem 0;
}
.ep-collections {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
}
.btn-sm {
  padding: 4px 10px;
  font-size: 0.8rem;
  margin-left: 0.5rem;
}
.danger {
  background: rgba(239, 68, 68, 0.2) !important;
  border-color: rgba(239, 68, 68, 0.5) !important;
  color: #fca5a5 !important;
}
.danger:hover {
  background: rgba(239, 68, 68, 0.4) !important;
}
.meta-badge {
  background: rgba(255,255,255,0.1);
  padding: 0.2rem 0.5rem;
  border-radius: 4px;
  font-size: 0.8rem;
  margin-left: 1rem;
}
.icon-only {
  padding: 4px 6px;
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
  .episode-list {
    grid-template-columns: 1fr;
  }
  .collection-item {
    flex-direction: column;
    align-items: flex-end;
    gap: 0.5rem;
  }
}
</style>
