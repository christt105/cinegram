// Runtime configuration for the SPA.
//
// Values are resolved with the following precedence:
//   1. window.__CINEGRAM__ — injected at container start by docker-entrypoint.sh
//      from the container environment, so a prebuilt image is reconfigurable.
//   2. import.meta.env.VITE_* — Vite build-time variables (used by the dev server).
//   3. Fallbacks derived from the browser host and the default service ports.

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
