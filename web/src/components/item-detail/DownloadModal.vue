<template>
  <div class="modal-overlay" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.7); display: flex; align-items: center; justify-content: center; z-index: 1000; backdrop-filter: blur(8px); padding: 1rem;">
    <div class="glass-panel" style="width: 100%; max-width: 460px; padding: 1.5rem; border-radius: 16px; background: rgba(17,24,39,0.95); border: 1px solid rgba(255,255,255,0.1); display: flex; flex-direction: column; gap: 1rem;">
      <div style="border-bottom: 1px solid rgba(255,255,255,0.08); padding-bottom: 0.75rem;">
        <h3 style="margin: 0; font-size: 1.2rem; color: #fff;">Download</h3>
        <p style="margin: 0.25rem 0 0 0; font-size: 0.85rem; color: #d6baff;">{{ collectionLabel(collection) }}</p>
      </div>
      <div>
        <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Version suffix (optional)</label>
        <input v-model="suffix" @keyup.enter="confirmDownload" type="text" placeholder="e.g. Erai BDRip" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
        <p style="margin: 0.5rem 0 0 0; font-size: 0.75rem; color: #a1a1aa;">
          Added to the filename so this version does not overwrite another one of the same movie.
          Jellyfin shows them as alternate versions.
        </p>
      </div>
      <div style="display: flex; justify-content: flex-end; gap: 0.75rem; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 0.75rem;">
        <button @click="emit('close')" class="glass-button">Cancel</button>
        <button @click="confirmDownload" :disabled="loading" class="glass-button primary">
          {{ loading ? 'Queueing…' : 'Download' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { apiUrl } from '../../config'
import { collectionLabel } from '../../utils/format'

const props = defineProps<{ collection: any }>()
const emit = defineEmits<{ close: [] }>()

const suffix = ref(props.collection.notes || '')
const loading = ref(false)

const confirmDownload = async () => {
  loading.value = true
  try {
    const res = await fetch(`${apiUrl}/downloads/enqueue/collection/${props.collection.id}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name_suffix: suffix.value.trim() || null })
    })
    if (res.ok) {
      const data = await res.json()
      alert(data.status === 'ignored' ? data.message : 'Download added to queue.')
      emit('close')
    } else {
      alert("Error adding download.")
    }
  } catch (err) {
    console.error(err)
    alert("Connection error.")
  } finally {
    loading.value = false
  }
}
</script>
