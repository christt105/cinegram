<template>
  <div class="settings-container" style="max-width: 1000px; margin: 0 auto; padding: 2rem;">
    <h1>System Settings</h1>
    
    <div class="settings-grid" style="display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1.5rem; margin-top: 1.5rem;">
      <!-- Connection Status Card -->
      <div class="glass-panel settings-card" style="padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem;">
        <h3>Connection Status</h3>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Jellyfin URL:</span>
          <span class="value" style="font-family: monospace;">{{ jellyfinUrl }}</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Jellyfin Auth:</span>
          <span class="value success-text" v-if="jellyfinItems && jellyfinItems.length > 0" style="color: #4ade80; font-weight: 600;">Authenticated</span>
          <span class="value error-text" v-else style="color: #f87171; font-weight: 600;">Disconnected / Missing Token</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Backend API URL:</span>
          <span class="value" style="font-family: monospace;">{{ backendUrl }}</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Telegram Bot Username:</span>
          <span class="value" style="color: #60a5fa; font-weight: 500;">@BibliotecaKachopinesBot</span>
        </div>
      </div>
      
      <!-- Library Statistics Card -->
      <div class="glass-panel settings-card" style="padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem;">
        <h3>Library Statistics</h3>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Movies in Telegram:</span>
          <span class="value" style="font-weight: 600;">{{ telegramMovies ? telegramMovies.length : 0 }}</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Series in Telegram:</span>
          <span class="value" style="font-weight: 600;">{{ telegramSeries ? telegramSeries.length : 0 }}</span>
        </div>
        <div class="status-item" style="display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 0.5rem;">
          <span class="label" style="color: var(--text-secondary);">Total Jellyfin Items:</span>
          <span class="value" style="font-weight: 600;">{{ jellyfinItems ? jellyfinItems.length : 0 }}</span>
        </div>
      </div>

      <!-- Telegram Link Configuration Card -->
      <div class="glass-panel settings-card" style="padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem;">
        <h3>Telegram Message Links</h3>
        <p style="color: var(--text-secondary); font-size: 0.85rem; margin: 0;">
          Configura el prefijo de enlace para poder ir directamente a los mensajes del archivo en Telegram desde la biblioteca.
        </p>
        <div>
          <label style="display: block; font-size: 0.85rem; color: #a1a1aa; margin-bottom: 0.25rem;">Base de enlace de Telegram:</label>
          <input v-model="telegramLinkBase" @change="saveTelegramLinkBase" type="text" placeholder="https://t.me/BibliotecaKachopinesBot o https://t.me/c/1234567890" style="width: 100%; padding: 8px 12px; border-radius: 8px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.12); color: #fff; font-family: monospace; font-size: 0.9rem;" />
        </div>
        <div style="font-size: 0.75rem; color: #6b7280; display: flex; flex-direction: column; gap: 0.25rem;">
          <span>• <strong>Grupo/Canal Privado:</strong> Usa <code>https://t.me/c/ID_DEL_CHAT</code> (sin el prefijo -100).</span>
          <span>• <strong>Chat del Bot:</strong> Usa <code>https://t.me/BibliotecaKachopinesBot</code>.</span>
        </div>
      </div>

      <!-- Maintenance and Anomalies Card -->
      <div class="glass-panel settings-card" style="grid-column: 1 / -1; margin-top: 1.5rem; padding: 1.5rem;">
        <h3 style="margin-bottom: 0.5rem;">Base de Datos y Mantenimiento de Integridad</h3>
        <p style="color: var(--text-secondary); margin-bottom: 1.5rem; font-size: 0.95rem;">
          Aquí se muestran colecciones o archivos que no se han podido asociar correctamente a ninguna película o serie de TMDB (registros huérfanos/anomalías).
        </p>

        <div v-if="loadingOrphans" class="loading-text" style="color: var(--text-secondary);">Cargando anomalías...</div>
        <div v-else-if="orphans.length === 0" class="success-text" style="display: flex; align-items: center; gap: 0.5rem; color: #4ade80; font-weight: 600;">
          <span>✓</span> ¡No se han encontrado anomalías ni colecciones huérfanas en la base de datos!
        </div>
        <div v-else class="orphans-section" style="display: flex; flex-direction: column; gap: 1rem;">
          <!-- Batch Actions Toolbar -->
          <div class="batch-toolbar glass-panel" style="padding: 0.75rem 1.25rem; display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; background: rgba(255, 255, 255, 0.03); border-radius: 12px; border-color: rgba(255,255,255,0.08);">
            <div style="display: flex; align-items: center; gap: 0.75rem;">
              <input type="checkbox" :checked="isAllSelected" @change="toggleSelectAll" style="width: 18px; height: 18px; cursor: pointer; accent-color: var(--jellyfin-blue);" />
              <span style="font-size: 0.95rem; font-weight: 500;">
                Seleccionados: <strong style="color: var(--jellyfin-blue);">{{ selectedOrphanIds.length }}</strong> de <strong>{{ orphans.length }}</strong>
              </span>
            </div>
            
            <div style="display: flex; gap: 0.75rem;">
              <button @click="identifyBatch" :disabled="selectedOrphanIds.length === 0" class="glass-button" style="background: rgba(59, 130, 246, 0.15); border-color: rgba(59, 130, 246, 0.35); color: #93c5fd; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;" :style="{ opacity: selectedOrphanIds.length === 0 ? 0.5 : 1 }">
                🔍 Identificar en Lote
              </button>
              <button @click="deleteBatch" :disabled="selectedOrphanIds.length === 0" class="glass-button danger" style="background: rgba(239, 68, 68, 0.15); border-color: rgba(239, 68, 68, 0.35); color: #fca5a5; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;" :style="{ opacity: selectedOrphanIds.length === 0 ? 0.5 : 1 }">
                🗑️ Borrar en Lote
              </button>
            </div>
          </div>

          <!-- Orphans List -->
          <div class="orphans-list" style="display: flex; flex-direction: column; gap: 0.75rem;">
            <div v-for="orphan in orphans" :key="orphan.id" class="orphan-item glass-panel" style="padding: 1rem; background: rgba(0, 0, 0, 0.2); display: flex; align-items: flex-start; gap: 1.25rem; border-radius: 12px;">
              <div style="padding-top: 0.25rem;">
                <input type="checkbox" :value="orphan.id" v-model="selectedOrphanIds" style="width: 18px; height: 18px; cursor: pointer; accent-color: var(--jellyfin-blue);" />
              </div>
              <div style="display: flex; flex-direction: column; gap: 0.25rem; flex-grow: 1;">
                <strong style="color: #fca5a5; font-size: 1.05rem;">{{ orphan.name }}</strong>
                <span style="font-size: 0.85rem; color: #a1a1aa;">
                  Colección ID: {{ orphan.id }} | Calidad: {{ orphan.quality || 'Automática' }} | {{ orphan.files_count }} archivos
                </span>
                <ul style="margin: 0.5rem 0 0 1rem; padding: 0; font-size: 0.85rem; color: #888;">
                  <li v-for="file in orphan.files" :key="file.id" style="list-style-type: square; margin-bottom: 0.25rem;">
                    {{ file.filename }} <span style="color: #555;">({{ (file.filesize / (1024 * 1024)).toFixed(1) }} MB)</span>
                  </li>
                </ul>
              </div>
              
              <div style="display: flex; gap: 0.5rem; align-self: center;">
                <button @click="identifyOrphan(orphan.id)" class="glass-button" style="background: rgba(59, 130, 246, 0.15); border-color: rgba(59, 130, 246, 0.35); color: #93c5fd; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;">
                  🔍 Identificar
                </button>
                <button @click="deleteOrphan(orphan.id)" class="glass-button danger" style="background: rgba(239, 68, 68, 0.15); border-color: rgba(239, 68, 68, 0.35); color: #fca5a5; padding: 6px 12px; font-size: 0.85rem; border-radius: 8px;">
                  🗑️ Borrar
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';

const props = defineProps<{
  jellyfinItems: any[];
  telegramMovies: any[];
  telegramSeries: any[];
}>();

const jellyfinUrl = import.meta.env.VITE_JELLYFIN_URL || 'http://192.168.1.15:8096';
const backendUrl = import.meta.env.VITE_BACKEND_URL || `${window.location.protocol}//${window.location.hostname}:8005`;

const telegramLinkBase = ref(localStorage.getItem('telegram_link_base') || 'https://t.me/BibliotecaKachopinesBot');
const saveTelegramLinkBase = () => {
  localStorage.setItem('telegram_link_base', telegramLinkBase.value.trim());
};

const orphans = ref<any[]>([]);
const loadingOrphans = ref(true);
const selectedOrphanIds = ref<number[]>([]);

const isAllSelected = computed(() => {
  return orphans.value.length > 0 && selectedOrphanIds.value.length === orphans.value.length;
});

const toggleSelectAll = (e: any) => {
  if (e.target.checked) {
    selectedOrphanIds.value = orphans.value.map(o => o.id);
  } else {
    selectedOrphanIds.value = [];
  }
};

const fetchOrphans = async () => {
  loadingOrphans.value = true;
  selectedOrphanIds.value = [];
  try {
    const res = await fetch(`${backendUrl}/maintenance/orphans`);
    if (res.ok) {
      orphans.value = await res.json();
    }
  } catch (err) {
    console.error(err);
  } finally {
    loadingOrphans.value = false;
  }
};

const identifyOrphan = async (colId: number) => {
  const tmdbIdStr = prompt("Introduce el ID de TMDB para identificar esta colección:");
  if (!tmdbIdStr) return;
  const tmdbId = intVal(tmdbIdStr.trim());
  if (isNaN(tmdbId)) {
    alert("El ID de TMDB debe ser un número válido.");
    return;
  }
  
  try {
    const res = await fetch(`${backendUrl}/collections/${colId}/identify`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ tmdb_id: tmdbId })
    });
    if (res.ok) {
      alert("Identificado y vinculado correctamente.");
      fetchOrphans();
    } else {
      const txt = await res.text();
      alert("Error al identificar: " + txt);
    }
  } catch (err) {
    console.error(err);
    alert("Error de conexión.");
  }
};

const identifyBatch = async () => {
  if (selectedOrphanIds.value.length === 0) return;
  const tmdbIdStr = prompt(`Introduce el ID de TMDB para identificar las ${selectedOrphanIds.value.length} colecciones seleccionadas de golpe:`);
  if (!tmdbIdStr) return;
  const tmdbId = intVal(tmdbIdStr.trim());
  if (isNaN(tmdbId)) {
    alert("El ID de TMDB debe ser un número válido.");
    return;
  }
  
  try {
    const res = await fetch(`${backendUrl}/maintenance/identify-batch`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        collection_ids: selectedOrphanIds.value,
        tmdb_id: tmdbId
      })
    });
    if (res.ok) {
      alert("Identificación en lote completada con éxito.");
      fetchOrphans();
    } else {
      alert("Error al procesar lote.");
    }
  } catch (err) {
    console.error(err);
    alert("Error de conexión.");
  }
};

const deleteBatch = async () => {
  if (selectedOrphanIds.value.length === 0) return;
  if (!confirm(`¿Seguro que quieres borrar las ${selectedOrphanIds.value.length} colecciones seleccionadas de golpe? Se eliminarán de forma permanente.`)) return;
  
  try {
    const res = await fetch(`${backendUrl}/maintenance/delete-batch`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        collection_ids: selectedOrphanIds.value
      })
    });
    if (res.ok) {
      alert("Borrado en lote completado.");
      fetchOrphans();
    } else {
      alert("Error al borrar lote.");
    }
  } catch (err) {
    console.error(err);
    alert("Error de conexión.");
  }
};

const intVal = (val: string) => {
  return parseInt(val);
};

const deleteOrphan = async (colId: number) => {
  if (!confirm("¿Seguro que quieres borrar esta colección huérfana de la base de datos? Se eliminarán también las referencias de sus archivos.")) return;
  try {
    const res = await fetch(`${backendUrl}/collections/${colId}`, {
      method: "DELETE"
    });
    if (res.ok) {
      alert("Colección huérfana eliminada.");
      fetchOrphans();
    } else {
      alert("Error al borrar.");
    }
  } catch (err) {
    console.error(err);
  }
};

onMounted(() => {
  fetchOrphans();
});
</script>
