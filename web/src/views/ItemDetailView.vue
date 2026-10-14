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
      <ItemHero
        :item="item"
        :type="type"
        :jellyfin-item-id="jellyfinItemId"
        :matching-with-jellyfin="matchingWithJellyfin"
        @reidentify="showingReidentifyModal = true"
        @match-jellyfin="matchWithJellyfin"
        @change-poster="showingPosterModal = true"
        @delete="deleteItem"
      />

      <div class="item-actions">
        <div v-if="movie" class="collections-section">
          <h2>Available Versions</h2>
          <div v-if="movie.collections && movie.collections.length > 0" class="collection-list">
            <CollectionCard
              v-for="col in movie.collections"
              :key="col.id"
              :collection="col"
              :selecting="selectingCollectionId === col.id"
              :selected-file-ids="selectedFileIds"
              @toggle-select="toggleSelectMode(col.id)"
              @toggle-file="toggleFileSelection"
              @download="downloadTarget = col"
              @probe="probeTarget = col"
              @reidentify="reidentifyTarget = col"
              @edit="openEdit(col, null, null)"
              @changed="fetchItem"
            />
          </div>
          <div v-else>No versions uploaded to Telegram yet.</div>
        </div>

        <div v-if="series" class="seasons-section">
          <h2 style="margin-bottom: 1.5rem; font-size: 2rem;">Seasons</h2>
          <SeasonSection
            v-for="season in series.seasons"
            :key="season.id"
            :series-id="id"
            :season="season"
            :expanded="expandedSeasonIds.has(season.id)"
            :selecting-collection-id="selectingCollectionId"
            :selected-file-ids="selectedFileIds"
            @toggle-expanded="toggleSeasonExpanded(season.id)"
            @toggle-select="toggleSelectMode"
            @toggle-file="toggleFileSelection"
            @download="downloadTarget = $event"
            @probe="probeTarget = $event"
            @reidentify="reidentifyTarget = $event"
            @edit="openEdit"
            @changed="fetchItem"
          />
        </div>
      </div>
    </div>

    <SelectionBar
      :open="selectingCollectionId !== null"
      :selected-count="selectedFileIds.length"
      :can-move="siblingCollections.length > 0"
      @delete="deleteSelectedFiles"
      @move="showingMoveModal = true"
      @new-collection="showingNewCollectionModal = true"
      @cancel="cancelSelection"
    />

    <DownloadModal v-if="downloadTarget" :collection="downloadTarget" @close="downloadTarget = null" />

    <ProbeModal
      v-if="probeTarget"
      :collection="probeTarget"
      :tmdb-id="item?.tmdb_id ?? null"
      :tvdb-id="series?.tvdb_id ?? null"
      @close="probeTarget = null"
      @probed="probeTarget = null; fetchItem()"
    />

    <MoveFilesModal
      v-if="showingMoveModal"
      :file-count="selectedFileIds.length"
      :collections="siblingCollections"
      @select="moveTo"
      @close="showingMoveModal = false"
    />

    <NewCollectionModal
      v-if="showingNewCollectionModal"
      :file-count="selectedFileIds.length"
      :create="moveSelectedToNewCollection"
      @close="showingNewCollectionModal = false"
    />

    <EditCollectionModal
      v-if="editTarget"
      :collection="editTarget.collection"
      :type="type"
      :season-number="editTarget.season"
      :episode-number="editTarget.episode"
      :vocabulary="metadataVocabulary"
      @close="editTarget = null"
      @saved="editTarget = null; fetchItem()"
    />

    <PosterModal
      v-if="showingPosterModal"
      :item-path="itemPath"
      :current-poster="item?.poster_path ?? null"
      :apply="setPoster"
      @close="showingPosterModal = false"
    />

    <ReidentifyItemModal
      v-if="showingReidentifyModal"
      :type="type"
      :title="movie?.title || series?.manual_title || ''"
      @select="reidentifyItem"
      @close="showingReidentifyModal = false"
    />

    <ReidentifyCollectionModal
      v-if="reidentifyTarget"
      :collection="reidentifyTarget"
      @close="reidentifyTarget = null"
      @reidentified="reidentifyTarget = null; fetchItem()"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { fetchMetadataVocabulary, type MetadataVocabulary, type TelegramCollection } from '../api/cinegram'
import { useCatalogItem } from '../composables/useCatalogItem'
import { useFileSelection } from '../composables/useFileSelection'
import ItemHero from '../components/item-detail/ItemHero.vue'
import CollectionCard from '../components/item-detail/CollectionCard.vue'
import SeasonSection from '../components/item-detail/SeasonSection.vue'
import SelectionBar from '../components/item-detail/SelectionBar.vue'
import DownloadModal from '../components/item-detail/DownloadModal.vue'
import ProbeModal from '../components/item-detail/ProbeModal.vue'
import MoveFilesModal from '../components/item-detail/MoveFilesModal.vue'
import NewCollectionModal from '../components/item-detail/NewCollectionModal.vue'
import EditCollectionModal from '../components/item-detail/EditCollectionModal.vue'
import PosterModal from '../components/item-detail/PosterModal.vue'
import ReidentifyItemModal from '../components/item-detail/ReidentifyItemModal.vue'
import ReidentifyCollectionModal from '../components/item-detail/ReidentifyCollectionModal.vue'

const props = defineProps<{
  type: string
  id: string
}>()

const {
  item,
  movie,
  series,
  isLoading,
  itemPath,
  jellyfinItemId,
  matchingWithJellyfin,
  fetchItem,
  reidentify,
  setPoster,
  matchWithJellyfin,
  deleteItem
} = useCatalogItem(props)

const {
  selectingCollectionId,
  selectedFileIds,
  toggleSelectMode,
  toggleFileSelection,
  cancelSelection,
  siblingCollections,
  deleteSelectedFiles,
  moveSelectedFiles,
  moveSelectedToNewCollection
} = useFileSelection(movie, series, fetchItem)

const metadataVocabulary = ref<MetadataVocabulary>({ qualities: [], codecs: [], languages: {} })

const loadMetadataVocabulary = async () => {
  try {
    metadataVocabulary.value = await fetchMetadataVocabulary()
  } catch (err) {
    console.error('Failed to load metadata vocabulary', err)
  }
}

const expandedSeasonIds = ref(new Set<number>())

const toggleSeasonExpanded = (seasonId: number) => {
  if (expandedSeasonIds.value.has(seasonId)) {
    expandedSeasonIds.value.delete(seasonId)
  } else {
    expandedSeasonIds.value.add(seasonId)
  }
}

watch(item, (data, previous) => {
  if (previous || !data || !('seasons' in data) || !data.seasons) return
  const latestSeason = data.seasons[data.seasons.length - 1]
  expandedSeasonIds.value = latestSeason ? new Set([latestSeason.id]) : new Set()
})

const downloadTarget = ref<TelegramCollection | null>(null)
const probeTarget = ref<TelegramCollection | null>(null)
const reidentifyTarget = ref<TelegramCollection | null>(null)
const editTarget = ref<{ collection: TelegramCollection; season: number | null; episode: number | null } | null>(null)
const showingMoveModal = ref(false)
const showingNewCollectionModal = ref(false)
const showingPosterModal = ref(false)
const showingReidentifyModal = ref(false)

const openEdit = (collection: TelegramCollection, season: number | null, episode: number | null) => {
  editTarget.value = { collection, season, episode }
}

const moveTo = async (collectionId: number) => {
  if (await moveSelectedFiles(collectionId)) showingMoveModal.value = false
}

const reidentifyItem = (tmdbId: number) => {
  showingReidentifyModal.value = false
  reidentify(tmdbId)
}

onMounted(() => {
  fetchItem()
  loadMetadataVocabulary()
})
</script>

<style scoped>
.item-detail-page {
  padding: 2rem;
  max-width: 1040px;
  margin: 0 auto;
  overflow-wrap: anywhere;
  word-break: break-word;
}
.header-nav {
  margin-bottom: 1.5rem;
}
.item-actions h2 {
  font-size: 1.5rem;
  font-weight: 700;
  letter-spacing: -0.01em;
  margin-bottom: 1rem;
}
.collection-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

@media (max-width: 768px) {
  .item-detail-page {
    padding: 1rem;
    overflow-x: hidden;
  }
}
</style>
