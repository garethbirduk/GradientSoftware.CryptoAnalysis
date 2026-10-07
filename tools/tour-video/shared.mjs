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

// The tour as the page plays it: from the local server when one is running there, which writes out the sections that explain
// something from the prices (see Explain in CryptoAnalysis.Site), else as it is in the file, where those have no texts yet.
export async function readTour(url = arg('url', 'http://localhost:5178')) {
  const served = await fetch(`${url}/api/tour`).then(r => r.ok ? r.json() : null).catch(() => null);
  if (served) return served;
  console.log(`No server at ${url}: texts written from the prices are not read.`);
  return JSON.parse(readFileSync(tourPath, 'utf8'));
}

// The texts that are read aloud: every level of detail a cue is written at, so the page has a clip whichever it shows. A
// text marked "voice": false is shown but not read.
export function tourTexts(tour) {
  const texts = new Map();
  for (const section of tour.sections ?? []) for (const cue of section.cues ?? []) if (cue.voice !== false)
    for (const text of Object.values(cue.texts ?? {})) if (text) texts.set(textKey(text), text);
  return texts;
}

// What the voice says in place of what is written, from the tour's glossary: "BoS" can be read as "Break of Structure" while
// the text on screen stays as it is. An entry stands for a whole word in the case it is written in, and the longest entry
// that fits is used, so "Break of Structure (BoS)" can have an entry of its own that does not say the name twice. An entry
// that says nothing, as "(BoS)" may, leaves no gap behind: a space before punctuation goes, and so do doubled spaces.
// Where an entry starts or ends with punctuation, as ":00" does, that side may touch a word, so the ":00" of "17:00" is
// read as "hundred", with a space put before it.
export function spokenText(tour) {
  const entries = Object.entries(tour.glossary ?? {}).map(([from, to]) => [from.trim(), String(to)]).filter(([from]) => from)
    .sort((a, b) => b[0].length - a[0].length);
  if (!entries.length) return text => text;
  const lookup = new Map(entries), escaped = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const wordy = /[\p{L}\p{N}_]/u, whole = from => `${wordy.test(from[0]) ? '(?<![\\p{L}\\p{N}_])' : ''}${escaped(from)}${wordy.test(from.at(-1)) ? '(?![\\p{L}\\p{N}_])' : ''}`;
  const words = new RegExp(entries.map(([from]) => whole(from)).join('|'), 'gu');
  const said = (match, at, text) => {
    const to = lookup.get(match);
    return to && wordy.test(to[0]) && wordy.test(text[at - 1] ?? '') ? ` ${to}` : to;
  };
  return text => text.replace(words, said).replace(/\s+([.,;:!?])/g, '$1').replace(/ {2,}/g, ' ').trim();
}

export function arg(name, fallback) {
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
}
