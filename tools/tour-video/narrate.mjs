// Reads every text in tour.json aloud with Kokoro (open source, runs locally; the model is fetched on first use) into
// wwwroot/tour-audio, one clip per text, and writes index.json there: the page uses a clip's length as the text's seconds and
// plays it when its Voice button is on. A text whose clip exists is skipped, so only changed texts are read again: changed
// on screen, or in what the tour's glossary has the voice say for them. A text marked "voice": false is not read.
// Usage: node narrate.mjs [--voice af_heart] [--force]
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { KokoroTTS } from 'kokoro-js';
import { arg, audioDir, readTour, spokenText, tourTexts } from './shared.mjs';

const voice = arg('voice', 'af_heart'), force = process.argv.includes('--force');
const tour = readTour(), texts = tourTexts(tour), spoken = spokenText(tour);
mkdirSync(audioDir, { recursive: true });

const indexPath = join(audioDir, 'index.json');
const index = existsSync(indexPath) && !force ? JSON.parse(readFileSync(indexPath, 'utf8')) : {};
let tts = null;

for (const [key, text] of texts) {
  const file = `${key}.wav`, said = spoken(text), was = index[key];
  if (was?.voice === voice && (was.spoken ?? was.text) === said && existsSync(join(audioDir, file)) && !force) continue;
  tts ??= await KokoroTTS.from_pretrained('onnx-community/Kokoro-82M-v1.0-ONNX', { dtype: 'q8' });
  const audio = await tts.generate(said, { voice });
  await audio.save(join(audioDir, file));
  index[key] = { text, ...(said !== text ? { spoken: said } : {}), file, voice, seconds: Math.round(audio.audio.length / audio.sampling_rate * 100) / 100 };
  console.log(`${key} ${index[key].seconds}s  ${said.slice(0, 60)}`);
}

// Clips of texts no longer in the tour are left on disk but dropped from the index.
for (const key of Object.keys(index)) if (!texts.has(key)) delete index[key];
writeFileSync(indexPath, JSON.stringify(index, null, 2) + '\n');
console.log(`${Object.keys(index).length} clips in ${audioDir}`);
