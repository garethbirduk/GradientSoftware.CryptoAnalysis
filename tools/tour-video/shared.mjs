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

// The texts that are read aloud: a text marked "voice": false is shown but not read.
export function tourTexts(tour) {
  const texts = new Map();
  for (const section of tour.sections ?? []) for (const cue of section.cues ?? []) if (cue.text && cue.voice !== false) texts.set(textKey(cue.text), cue.text);
  return texts;
}

// What the voice says in place of what is written, from the tour's glossary: "BoS" can be read as "Break of Structure" while
// the text on screen stays as it is. An entry stands for a whole word in the case it is written in, and the longest entry
// that fits is used, so "Break of Structure (BoS)" can have an entry of its own that does not say the name twice. An entry
// that says nothing, as "(BoS)" may, leaves no gap behind: a space before punctuation goes, and so do doubled spaces.
export function spokenText(tour) {
  const entries = Object.entries(tour.glossary ?? {}).map(([from, to]) => [from.trim(), String(to)]).filter(([from]) => from)
    .sort((a, b) => b[0].length - a[0].length);
  if (!entries.length) return text => text;
  const lookup = new Map(entries), escaped = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const words = new RegExp(`(?<![\\p{L}\\p{N}_])(?:${entries.map(([from]) => escaped(from)).join('|')})(?![\\p{L}\\p{N}_])`, 'gu');
  return text => text.replace(words, match => lookup.get(match)).replace(/\s+([.,;:!?])/g, '$1').replace(/ {2,}/g, ' ').trim();
}

export function arg(name, fallback) {
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
}
