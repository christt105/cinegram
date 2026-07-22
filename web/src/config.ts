// Resolves config from window.__CINEGRAM__ (runtime), then VITE_* (build), then
// the browser host with default ports.

type RuntimeConfig = {
  JELLYFIN_URL?: string;
  JELLYFIN_TOKEN?: string;
  BACKEND_URL?: string;
  BACKEND_PORT?: string;
  BOT_NET_URL?: string;
  BOT_NET_PORT?: string;
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

const backendPort =
  resolve(runtime.BACKEND_PORT, import.meta.env.VITE_BACKEND_PORT) || '8005';
const botNetPort =
  resolve(runtime.BOT_NET_PORT, import.meta.env.VITE_BOT_NET_PORT) || '8088';

export const backendUrl =
  resolve(runtime.BACKEND_URL, import.meta.env.VITE_BACKEND_URL) ||
  `${protocol}//${host}:${backendPort}`;

export const botNetUrl =
  resolve(runtime.BOT_NET_URL, import.meta.env.VITE_BOT_NET_URL) ||
  `${protocol}//${host}:${botNetPort}`;

export const jellyfinUrl =
  resolve(runtime.JELLYFIN_URL, import.meta.env.VITE_JELLYFIN_URL) ||
  `${protocol}//${host}:8096`;

export const jellyfinToken = resolve(
  runtime.JELLYFIN_TOKEN,
  import.meta.env.VITE_JELLYFIN_TOKEN
);

export const appVersion =
  (import.meta.env.VITE_APP_VERSION as string | undefined)?.trim() || 'dev';
