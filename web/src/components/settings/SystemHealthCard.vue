<template>
  <div class="glass-panel settings-card health-card">
    <div class="health-header">
      <h3>Bot Status</h3>
      <span v-if="report" class="badge" :class="`tone-${verdictTone[report.status]}`">{{ verdictLabel[report.status] }}</span>
    </div>

    <p v-if="error" class="message tone-bad">{{ error }}</p>

    <template v-if="report">
      <ul v-if="report.problems.length || report.warnings.length" class="messages">
        <li v-for="problem in report.problems" :key="problem" class="message tone-bad">{{ problem }}</li>
        <li v-for="warning in report.warnings" :key="warning" class="message tone-warn">{{ warning }}</li>
      </ul>

      <div class="status-row">
        <span class="label">Telegram bot</span>
        <span class="value" :class="`tone-${botTone}`">{{ botLabel }}</span>
      </div>
      <div v-if="report.account" class="status-row">
        <span class="label">Account session</span>
        <span class="value" :class="`tone-${accountTone[report.account.session]}`">{{ accountLabel }}</span>
      </div>
      <div v-for="loop in report.loops" :key="loop.name" class="status-row" :title="loop.problem ?? undefined">
        <span class="label">{{ loopLabel[loop.name] ?? loop.name }}</span>
        <span class="value" :class="`tone-${loopTone[loop.state]}`">{{ loopStateLabel[loop.state] }}</span>
      </div>
    </template>
    <p v-else-if="!error" class="label">Loading…</p>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { fetchHealth } from '../../api/cinegram';
import type { AccountSession, BotState, HealthReport, HealthVerdict, LoopState } from '../../api/cinegram';

type Tone = 'good' | 'warn' | 'bad' | 'muted';

const verdictLabel: Record<HealthVerdict, string> = {
  ok: 'Healthy',
  starting: 'Starting',
  disabled: 'Bot disabled',
  unhealthy: 'Unhealthy',
};
const verdictTone: Record<HealthVerdict, Tone> = { ok: 'good', starting: 'warn', disabled: 'muted', unhealthy: 'bad' };

const botStateLabel: Record<BotState, string> = {
  starting: 'Starting',
  ready: 'Connected',
  failed: 'Failed to start',
  disabled: 'Disabled',
};

const accountStateLabel: Record<AccountSession, string> = {
  not_checked: 'Not checked yet',
  restored: 'Restored',
  missing: 'No saved session',
  failed: 'Could not resume',
};
const accountTone: Record<AccountSession, Tone> = { not_checked: 'muted', restored: 'good', missing: 'warn', failed: 'warn' };

const loopLabel: Record<string, string> = {
  Downloader: 'Downloads',
  Uploader: 'Uploads',
  WatchedFolder: 'Downloads folder watcher',
  WatchResume: 'Watch resume',
  Queue: 'Task queue',
  JellyfinBackup: 'Jellyfin backup',
};
const loopStateLabel: Record<LoopState, string> = {
  waiting: 'Waiting',
  running: 'Running',
  done: 'Done',
  skipped: 'Not started',
  stopped: 'Stopped',
  crashed: 'Crashed',
};
const loopTone: Record<LoopState, Tone> = {
  waiting: 'warn',
  running: 'good',
  done: 'good',
  skipped: 'muted',
  stopped: 'bad',
  crashed: 'bad',
};

const report = ref<HealthReport | null>(null);
const error = ref<string | null>(null);

const botLabel = computed(() => {
  const bot = report.value!.bot;
  if (bot.state === 'ready' && bot.connected === false) return 'Disconnected';
  return botStateLabel[bot.state];
});

const botTone = computed<Tone>(() => {
  const bot = report.value!.bot;
  if (bot.state === 'ready') return bot.connected === false ? 'bad' : 'good';
  return { starting: 'warn', failed: 'bad', disabled: 'muted' }[bot.state] as Tone;
});

const accountLabel = computed(() => {
  const account = report.value!.account!;
  const label = accountStateLabel[account.session];
  return account.session === 'restored' && account.premium ? `${label} (Premium)` : label;
});

const load = async () => {
  try {
    report.value = await fetchHealth();
    error.value = null;
  } catch (err) {
    console.error(err);
    error.value = 'Could not reach the server.';
  }
};

let timer: ReturnType<typeof setInterval> | null = null;

onMounted(() => {
  load();
  timer = setInterval(load, 15000);
});

onUnmounted(() => {
  if (timer) clearInterval(timer);
});
</script>

<style scoped>
.health-card {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.health-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.health-header h3 {
  margin: 0;
}

.badge {
  padding: 0.2rem 0.7rem;
  border-radius: 999px;
  font-size: 0.85rem;
  font-weight: 600;
  background: rgba(255, 255, 255, 0.06);
}

.status-row {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  padding-bottom: 0.5rem;
}

.label {
  color: var(--text-secondary);
}

.value {
  font-weight: 600;
  text-align: right;
}

.messages {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.message {
  margin: 0;
  font-size: 0.9rem;
  overflow-wrap: anywhere;
}

.tone-good { color: #4ade80; }
.tone-warn { color: #fbbf24; }
.tone-bad { color: #f87171; }
.tone-muted { color: var(--text-secondary); }
</style>
