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
        <img v-if="item.poster_path" :src="item.poster_path" class="hero-poster" />
        <div class="hero-info">
          <h1>{{ item.Name || 'Unknown Title' }}</h1>
          <p class="year">{{ item.ProductionYear }}</p>
          <p class="overview">{{ item.Overview }}</p>
          
          <div style="margin-top: 1rem;" v-if="type === 'movies'">
            <div v-if="item.MediaSources && item.MediaSources.length > 1" style="display:flex; flex-direction:column; gap:0.5rem;">
              <h3 style="font-size: 1.1rem; color: #a1a1aa; margin: 0;">Versiones disponibles:</h3>
              <div v-for="ms in item.MediaSources" :key="ms.Id" style="display:flex; justify-content:space-between; align-items:center; background: rgba(0,0,0,0.2); padding: 0.5rem 1rem; border-radius: 8px;">
                <span style="font-size: 0.9rem;">{{ ms.Name || 'Versión alternativa' }}</span>
                <button @click="uploadItem(item, 'movie', ms.Path)" class="glass-button primary btn-sm">
                  <UploadCloud :size="14" style="margin-right:0.25rem;" /> Subir
                </button>
              </div>
            </div>
            <div v-else>
              <button @click="uploadItem(item, 'movie')" class="glass-button primary">
                <UploadCloud :size="16" /> Subir a Telegram
              </button>
            </div>
          </div>
        </div>
      </div>

      <div class="item-actions">
        <!-- Series have seasons and episodes -->
        <div v-if="type === 'series'" class="seasons-section">
          <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom: 1.5rem;">
            <h2 style="font-size: 2rem; margin:0;">Temporadas</h2>
            <button @click="uploadItem(item, 'series')" class="glass-button primary">
               <UploadCloud :size="16" /> Subir Serie Completa
            </button>
          </div>
          
          <div v-for="season in item.seasons" :key="season.Id" class="season-block">
            
            <div class="season-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid rgba(255,255,255,0.1); flex-wrap: wrap; gap: 1rem;">
              <h3 style="font-size: 1.5rem; margin: 0;">{{ season.Name }}</h3>
              <button @click="uploadItem(season, 'series')" class="glass-button primary">
                <UploadCloud :size="16" /> Subir Temporada
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
                      <span style="font-size:0.75rem; color:#a1a1aa; max-width: 150px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;" :title="ms.Name">{{ ms.Name || 'Versión' }}</span>
                      <button @click="uploadItem(ep, 'series', ms.Path)" class="glass-button primary btn-sm icon-only" title="Subir Versión">
                        <UploadCloud :size="14" />
                      </button>
                    </div>
                  </div>
                  <div v-else>
                    <button @click="uploadItem(ep, 'series')" class="glass-button primary btn-sm icon-only" title="Subir Episodio">
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

const backendUrl = import.meta.env.VITE_BACKEND_URL || 'http://192.168.1.15:8005'

const fetchItem = async () => {
  isLoading.value = true
  try {
    const data = await fetchItemDetails(props.id, props.type as 'movie' | 'series')
    if (data.ImageTags && data.ImageTags.Primary) {
      data.poster_path = getImageUrl(data.Id, data.ImageTags.Primary, 500)
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
  } catch (err) {
    console.error(err)
  } finally {
    isLoading.value = false
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
            alert("Subida añadida a la cola de Telegram.");
        } else {
            alert("Error al encolar subida.");
        }
    } catch (e) {
        console.error(e);
        alert("Error al encolar subida.");
    }
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
}

.header-nav {
  display: flex;
  align-items: center;
}

.item-hero {
  display: flex;
  gap: 2rem;
  margin-bottom: 2rem;
  align-items: flex-start;
  background: rgba(255, 255, 255, 0.05);
}

.hero-poster {
  width: 250px;
  border-radius: 12px;
  box-shadow: 0 10px 30px rgba(0,0,0,0.5);
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

@media (max-width: 768px) {
  .item-hero {
    flex-direction: column;
    align-items: center;
    text-align: center;
  }

  .hero-poster {
    width: 200px;
  }
}
</style>
