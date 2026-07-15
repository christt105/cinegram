<template>
  <div class="item-detail-page">
    <div class="header-nav">
      <button @click="$router.back()" class="glass-button">
        &larr; Volver
      </button>
    </div>

    <div v-if="isLoading" class="loading-state">Loading...</div>
    <div v-else-if="item" class="item-content glass-panel">
      
      <div class="item-hero">
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
                <span v-if="col.technical_metadata" class="meta-badge">Info. Técnica Disponible</span>
              </div>
              <div class="col-actions">
                <button @click="downloadCollection(col.id)" class="glass-button primary">Descargar a Jellyfin</button>
                <button @click="deleteCollection(col.id)" class="glass-button danger">Borrar de DB</button>
              </div>
            </div>
          </div>
          <div v-else>No versions uploaded to Telegram yet.</div>
        </div>

        <!-- Series have seasons and episodes -->
        <div v-if="type === 'series'" class="seasons-section">
          <h2>Seasons</h2>
          <div v-for="season in item.seasons" :key="season.id" class="season-block">
            <h3>Season {{ season.season_number }}</h3>
            
            <div class="episode-list">
              <div v-for="ep in season.episodes" :key="ep.id" class="episode-item glass-panel">
                <div class="ep-info">
                  <strong>Episode {{ ep.episode_number }}</strong>
                  <span>{{ ep.title || 'Unknown' }}</span>
                </div>
                
                <div v-if="ep.collections && ep.collections.length > 0" class="ep-collections">
                  <div v-for="col in ep.collections" :key="col.id" class="collection-item">
                    <span>{{ col.quality }}</span>
                    <button @click="downloadCollection(col.id)" class="glass-button primary btn-sm">Descargar</button>
                    <button @click="deleteCollection(col.id)" class="glass-button danger btn-sm">Borrar</button>
                  </div>
                </div>
                <div v-else class="no-col" style="color: var(--text-secondary); font-size: 0.9rem;">No respaldado aún</div>
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
      item.value = await res.json()
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
      alert("Download added to queue!")
    } else {
      alert("Failed to enqueue download.")
    }
  } catch (err) {
    console.error(err)
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
.collection-list, .episode-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
.collection-item, .episode-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem;
}
.ep-collections {
  display: flex;
  gap: 1rem;
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

@media (max-width: 768px) {
  .item-hero {
    flex-direction: column;
  }
  .hero-poster {
    width: 100%;
    max-width: 300px;
    margin: 0 auto;
  }
}
</style>
