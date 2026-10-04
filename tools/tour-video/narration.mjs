// Reads a tour's texts aloud with Kokoro (open source, runs locally; the model is fetched on first use) into
// wwwroot/tour-audio, one clip per text, and writes index.json there: the page uses a clip's length as the text's seconds and
// plays it when its Voice button is on. A text whose clip exists is skipped, so only changed texts are read again: changed
// on screen, or in what the tour's glossary has the voice say for them. A text marked "voice": false is not read.
// Each text is read a sentence at a time, and every sentence read is kept under tour-audio/sentences by its own hash, so a
// sentence that comes again, in another text or another tour, is not read twice: a text's clip is its sentences joined with
// a short pause. The model stays loaded for as long as the process lives (see worker.mjs).
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { KokoroTTS } from 'kokoro-js';
import { audioDir, spokenText, textKey, tourTexts } from './shared.mjs';

const sentenceDir = join(audioDir, 'sentences');
// Silence between sentences of a text, in seconds.
const pause = 0.3;
let tts = null;

// Full precision: on the CPU it reads about two and a half times faster than the 8-bit model (0.4 of the clip's length
// against 1.1), and sounds best. The model is a one-off download of a few hundred megabytes.
async function model() {
  tts ??= await KokoroTTS.from_pretrained('onnx-community/Kokoro-82M-v1.0-ONNX', { dtype: 'fp32' });
  return tts;
}

// A text as sentences: split after a full stop, question or exclamation mark followed by a space. A price never has
// decimals in the tour, so a full stop inside a sentence does not happen.
export const sentences = text => text.split(/(?<=[.!?])\s+/).map(s => s.trim()).filter(Boolean);

// The samples of one sentence in a voice, read now or found under tour-audio/sentences.
async function sentenceSamples(sentence, voice) {
  const file = join(sentenceDir, `${voice}-${textKey(sentence)}.f32`);
  if (existsSync(file)) {
    const raw = readFileSync(file);
    return { samples: new Float32Array(raw.buffer, raw.byteOffset, raw.byteLength / 4), read: false };
  }
  const audio = await (await model()).generate(sentence, { voice });
  const samples = audio.audio;
  mkdirSync(sentenceDir, { recursive: true });
  writeFileSync(file, Buffer.from(samples.buffer, samples.byteOffset, samples.byteLength));
  return { samples, read: true, rate: audio.sampling_rate };
}

// 16-bit PCM WAV of mono samples.
function wav(samples, rate) {
  const buf = Buffer.alloc(44 + samples.length * 2);
  buf.write('RIFF', 0); buf.writeUInt32LE(36 + samples.length * 2, 4); buf.write('WAVE', 8);
  buf.write('fmt ', 12); buf.writeUInt32LE(16, 16); buf.writeUInt16LE(1, 20); buf.writeUInt16LE(1, 22);
  buf.writeUInt32LE(rate, 24); buf.writeUInt32LE(rate * 2, 28); buf.writeUInt16LE(2, 32); buf.writeUInt16LE(16, 34);
  buf.write('data', 36); buf.writeUInt32LE(samples.length * 2, 40);
  for (let i = 0; i < samples.length; i++) buf.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round(samples[i] * 32767))), 44 + i * 2);
  return buf;
}

// The sample rate Kokoro speaks at, learnt from the first sentence it reads; sentences kept on disk were read at it too.
let sampleRate = 24000;

// Reads the texts of a tour that have no clip yet, or whose clip was read from other words, and brings index.json up to
// date. Returns how many clips the tour has and how many sentences were read now. log gets a line per text read.
export async function narrate(tour, { voice = 'af_heart', force = false, log = () => {} } = {}) {
  const texts = tourTexts(tour), spoken = spokenText(tour);
  mkdirSync(audioDir, { recursive: true });
  const indexPath = join(audioDir, 'index.json');
  const index = existsSync(indexPath) && !force ? JSON.parse(readFileSync(indexPath, 'utf8')) : {};
  let readNow = 0;

  for (const [key, text] of texts) {
    const file = `${key}.wav`, said = spoken(text), was = index[key];
    if (was?.voice === voice && (was.spoken ?? was.text) === said && existsSync(join(audioDir, file)) && !force) continue;
    const parts = [];
    for (const sentence of sentences(said)) {
      const part = await sentenceSamples(sentence, voice);
      if (part.rate) sampleRate = part.rate;
      if (part.read) readNow++;
      parts.push(part.samples);
    }
    const gap = Math.round(pause * sampleRate), total = parts.reduce((n, p) => n + p.length, 0) + gap * Math.max(0, parts.length - 1);
    const samples = new Float32Array(total);
    let at = 0;
    for (const [i, p] of parts.entries()) {
      samples.set(p, at);
      at += p.length + (i < parts.length - 1 ? gap : 0);
    }
    writeFileSync(join(audioDir, file), wav(samples, sampleRate));
    index[key] = { text, ...(said !== text ? { spoken: said } : {}), file, voice, seconds: Math.round(samples.length / sampleRate * 100) / 100 };
    log(`${key} ${index[key].seconds}s  ${said.slice(0, 60)}`);
  }

  // Clips of texts not in this tour stay in the index: another tour, written from the prices, may be using them.
  writeFileSync(indexPath, JSON.stringify(index, null, 2) + '\n');
  return { clips: Object.keys(index).length, read: readNow };
}
