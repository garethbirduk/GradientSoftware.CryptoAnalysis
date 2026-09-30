// Reads every text in tour.json aloud with Kokoro (open source, runs locally; the model is fetched on first use) into
// wwwroot/tour-audio, one clip per text, and writes index.json there: the page uses a clip's length as the text's seconds and
// plays it when its Voice button is on. A text whose clip exists is skipped, so only changed texts are read again.
// Usage: node narrate.mjs [--voice af_heart] [--force]
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { KokoroTTS } from 'kokoro-js';
import { arg, audioDir, readTour, textKey, tourTexts } from './shared.mjs';

const voice = arg('voice', 'af_heart'), force = process.argv.includes('--force');
const texts = tourTexts(readTour());
mkdirSync(audioDir, { recursive: true });

const indexPath = join(audioDir, 'index.json');
const index = existsSync(indexPath) && !force ? JSON.parse(readFileSync(indexPath, 'utf8')) : {};
let tts = null;

for (const [key, text] of texts) {
  const file = `${key}.wav`;
  if (index[key]?.voice === voice && existsSync(join(audioDir, file)) && !force) continue;
  tts ??= await KokoroTTS.from_pretrained('onnx-community/Kokoro-82M-v1.0-ONNX', { dtype: 'q8' });
  const audio = await tts.generate(text, { voice });
  await audio.save(join(audioDir, file));
  index[key] = { text, file, voice, seconds: Math.round(audio.audio.length / audio.sampling_rate * 100) / 100 };
  console.log(`${key} ${index[key].seconds}s  ${text.slice(0, 60)}`);
}

// Clips of texts no longer in the tour are left on disk but dropped from the index.
for (const key of Object.keys(index)) if (!texts.has(key)) delete index[key];
writeFileSync(indexPath, JSON.stringify(index, null, 2) + '\n');
console.log(`${Object.keys(index).length} clips in ${audioDir}`);
