import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
export const tourPath = join(repoRoot, 'CryptoAnalysis.Site', 'wwwroot', 'tour.json');
export const audioDir = join(repoRoot, 'CryptoAnalysis.Site', 'wwwroot', 'tour-audio');
export const outDir = join(repoRoot, 'artifacts', 'tour-video');

// The same short hash of a text the page uses to find its clip (FNV-1a, 32 bits, hex).
export function textKey(text) {
  let h = 0x811c9dc5;
  for (let i = 0; i < text.length; i++) { h ^= text.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; }
  return h.toString(16).padStart(8, '0');
}

export function readTour() {
  return JSON.parse(readFileSync(tourPath, 'utf8'));
}

export function tourTexts(tour) {
  const texts = new Map();
  for (const section of tour.sections ?? []) for (const cue of section.cues ?? []) if (cue.text) texts.set(textKey(cue.text), cue.text);
  return texts;
}

export function arg(name, fallback) {
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
}
