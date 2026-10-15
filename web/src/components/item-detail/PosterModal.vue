<template>
  <BaseModal title="Choose Poster from TMDB" @close="emit('close')">
    <div v-if="loading" style="color: #a1a1aa; text-align: center; padding: 2rem;">
      Loading available TMDB posters...
    </div>
    <div v-else-if="posters.length === 0" style="color: #a1a1aa; text-align: center; padding: 1.5rem;">
      No TMDB posters found for this item.
    </div>
    <div v-else class="posters-grid" style="display: grid; grid-template-columns: repeat(auto-fill, minmax(110px, 1fr)); gap: 0.75rem; max-height: 45vh; overflow-y: auto; padding-right: 0.5rem;">
      <div v-for="path in posters" :key="path" @click="select(path)" class="poster-option" style="cursor: pointer; border-radius: 8px; overflow: hidden; border: 3px solid transparent; transition: all 0.2s; position: relative;" :style="{ borderColor: currentPoster === path ? 'var(--jellyfin-blue)' : 'transparent' }">
        <img :src="'https://image.tmdb.org/t/p/w185' + path" style="width: 100%; display: block;" />
        <div v-if="currentPoster === path" style="position: absolute; top: 4px; right: 4px; background: var(--jellyfin-blue); color: white; border-radius: 50%; width: 20px; height: 20px; display: flex; align-items: center; justify-content: center; font-size: 0.75rem; font-weight: bold;">✓</div>
      </div>
    </div>

    <div style="border-top: 1px solid rgba(255,255,255,0.08); padding-top: 1rem; display: flex; flex-direction: column; gap: 0.5rem;">
      <label style="font-size: 0.85rem; color: #a1a1aa;">Or enter a poster URL manually:</label>
      <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
        <input v-model="manualUrl" type="text" placeholder="https://... o /path.jpg" style="flex-grow: 1; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
        <button @click="select(manualUrl)" class="glass-button primary">Apply</button>
      </div>
    </div>

    <template #footer>
      <button @click="emit('close')" class="glass-button">Close</button>
    </template>
  </BaseModal>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { apiUrl } from '../../config'
import BaseModal from '../BaseModal.vue'

const props = defineProps<{
  itemPath: string
  currentPoster: string | null
  apply: (path: string) => Promise<boolean>
}>()

const emit = defineEmits<{ close: [] }>()

const loading = ref(true)
const posters = ref<string[]>([])
const manualUrl = ref(props.currentPoster || '')

const select = async (path: string) => {
  if (!path.trim()) return
  if (await props.apply(path.trim())) emit('close')
}

onMounted(async () => {
  try {
    const res = await fetch(`${apiUrl}${props.itemPath}/posters`)
    if (res.ok) {
      posters.value = await res.json()
    }
  } catch (err) {
    console.error(err)
  } finally {
    loading.value = false
  }
})
</script>

<style scoped>
.poster-option:hover {
  transform: scale(1.05);
  box-shadow: 0 4px 12px rgba(0,0,0,0.5);
}
</style>
