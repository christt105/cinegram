// Resolves config from window.__CINEGRAM__ (runtime), then VITE_* (build), then
// defaults: the backend and bot-net are reached through the web's own origin, where
// nginx (or the Vite dev server) proxies /api and /bot to them.

type RuntimeConfig = {
  JELLYFIN_URL?: string;
  JELLYFIN_TOKEN?: string;
  BACKEND_URL?: string;
  BOT_NET_URL?: string;
};

declare global {
  interface Window {
    __CINEGRAM__?: RuntimeConfig;
  }
}

const runtime: RuntimeConfig =
  (typeof window !== 'undefined' && window.__CINEGRAM__) || {};

function resolve(runtimeValue?: string, buildValue?: unknown): string {
  const r = (runtimeValue ?? '').trim();
  if (r) return r;
  return (typeof buildValue === 'string' ? buildValue : '').trim();
}

const protocol = window.location.protocol;
const host = window.location.hostname;

export const backendUrl =
  resolve(runtime.BACKEND_URL, import.meta.env.VITE_BACKEND_URL) || '/api';

export const botNetUrl =
  resolve(runtime.BOT_NET_URL, import.meta.env.VITE_BOT_NET_URL) || '/bot';

export const jellyfinUrl =
  resolve(runtime.JELLYFIN_URL, import.meta.env.VITE_JELLYFIN_URL) ||
  `${protocol}//${host}:8096`;

export const jellyfinToken = resolve(
  runtime.JELLYFIN_TOKEN,
  import.meta.env.VITE_JELLYFIN_TOKEN
);

export const appVersion =
  (import.meta.env.VITE_APP_VERSION as string | undefined)?.trim() || 'dev';
