<template>
  <BaseModal title="Download" :subtitle="collectionLabel(collection)" max-width="460px" @close="emit('close')">
    <div>
      <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Version suffix (optional)</label>
      <input v-model="suffix" @keyup.enter="confirmDownload" type="text" placeholder="e.g. Erai BDRip" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff;" />
      <p style="margin: 0.5rem 0 0 0; font-size: 0.75rem; color: #a1a1aa;">
        Added to the filename so this version does not overwrite another one of the same movie.
        Jellyfin shows them as alternate versions.
      </p>
    </div>
    <template #footer>
      <button @click="emit('close')" class="glass-button">Cancel</button>
      <button @click="confirmDownload" :disabled="loading" class="glass-button primary">
        {{ loading ? 'Queueing…' : 'Download' }}
      </button>
    </template>
  </BaseModal>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { apiUrl } from '../../config'
import { collectionLabel } from '../../utils/format'
import BaseModal from '../BaseModal.vue'
import type { TelegramCollection } from '../../api/cinegram'

const props = defineProps<{ collection: TelegramCollection }>()
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
