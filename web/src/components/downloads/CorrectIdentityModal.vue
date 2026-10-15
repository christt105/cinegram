<template>
  <BaseModal
    title="Correct identity"
    :subtitle="targetRow ? targetRow.filename : `Applying to ${selectedCount} selected file(s)`"
    @close="emit('close')"
  >
    <TmdbSearchPanel
      v-show="step === 'search'"
      media-type="multi"
      :initial-query="targetRow ? targetRow.guess_title || targetRow.filename : ''"
      @select="selectResult"
    />

    <div v-if="step === 'episode-select'" class="episode-select">
      <p class="linking">
        Linking to series: <strong>{{ pendingTitle }}</strong>
      </p>
      <p class="episode-hint">Use season 0 for specials and OVAs.</p>
      <div class="episode-fields">
        <div class="field">
          <label>Season</label>
          <input type="number" v-model.number="season" min="0" />
        </div>
        <div class="field">
          <label>Episode <span class="field-hint">(empty = season pack)</span></label>
          <input type="number" v-model.number="episode" min="1" placeholder="—" />
        </div>
      </div>
    </div>

    <template #footer>
      <template v-if="step === 'episode-select'">
        <button class="glass-button" @click="step = 'search'">&larr; Back</button>
        <button class="glass-button success" :disabled="saving" @click="applyCorrection">
          {{ saving ? 'Saving…' : 'Confirm' }}
        </button>
      </template>
      <button v-else class="glass-button" @click="emit('close')">Close</button>
    </template>
  </BaseModal>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { correctWatchedFile, type WatchedFile } from '../../api/cinegram'
import type { TmdbSearchResult } from '../../composables/useTmdbSearch'
import BaseModal from '../BaseModal.vue'
import TmdbSearchPanel from '../tmdb/TmdbSearchPanel.vue'

/** With a target row it corrects that file; without one it hands the picked title back for the selection. */
const props = defineProps<{
  targetRow: WatchedFile | null
  selectedCount: number
}>()

const emit = defineEmits<{
  close: []
  corrected: []
  'batch-select': [result: TmdbSearchResult]
}>()

const step = ref<'search' | 'episode-select'>('search')
const pendingTmdbId = ref(0)
const pendingTitle = ref('')
const season = ref(props.targetRow?.guess_season ?? 1)
const episode = ref<number | null>(props.targetRow?.guess_episode ?? null)
const saving = ref(false)

const selectResult = async (result: TmdbSearchResult) => {
  if (!props.targetRow) {
    emit('batch-select', result)
    return
  }

  pendingTmdbId.value = result.id
  pendingTitle.value = result.title
  if (result.media_type === 'tv') {
    step.value = 'episode-select'
  } else {
    await applyCorrection()
  }
}

const applyCorrection = async () => {
  const row = props.targetRow
  if (!row) return
  saving.value = true
  try {
    await correctWatchedFile(row.id, pendingTmdbId.value, season.value, episode.value)
    emit('corrected')
  } catch (err) {
    console.error(err)
    alert(`Failed to correct ${row.filename}.`)
  } finally {
    saving.value = false
  }
}
</script>

<style scoped src="./buttons.css"></style>
<style scoped>
.linking {
  margin: 0;
  font-size: 0.85rem;
  color: var(--secondary);
}

.episode-select {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  flex-grow: 1;
}

.episode-hint {
  margin: 0;
  font-size: 0.8rem;
  color: #a1a1aa;
}

.episode-fields {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  flex: 1;
  min-width: 120px;
}

.field label {
  font-size: 0.8rem;
  color: #a1a1aa;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.field-hint {
  font-weight: 400;
  text-transform: none;
}

.field input {
  padding: 10px 14px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.12);
  color: #fff;
  font-size: 1rem;
  width: 100%;
  box-sizing: border-box;
}

</style>
