/**
 * Normalizes text for accent-insensitive comparisons: strips diacritics and
 * maps special separators (middle dot, en/em dash, hyphen) to spaces,
 * collapsing whitespace and lowercasing. Mirrors the bot-net TextNormalizer so
 * local search behaves the same on the web and in Telegram.
 */
export function normalizeText(text: string | null | undefined): string {
  if (!text) return '';
  return text
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[\u00b7\u2013\u2014\u002d]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();
}
