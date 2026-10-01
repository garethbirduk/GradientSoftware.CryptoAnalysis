// Plays the tour in a headless browser against the local server, records it, then lays the narration clips over the
// recording at the moments each text appeared and writes an MP4 with a chapter for each of the tour's chapters.
// Needs the local server (./build-site.ps1 -Serve), the clips from narrate.mjs, and ffmpeg on the path.
// Usage: node record.mjs [--url http://localhost:5178] [--width 1600] [--height 900] [--out artifacts/tour-video/tour.mp4]
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { chromium } from 'playwright';
import { arg, audioDir, outDir, readTour } from './shared.mjs';

const url = arg('url', 'http://localhost:5178'), width = Number(arg('width', 1600)), height = Number(arg('height', 900));
const out = arg('out', join(outDir, 'tour.mp4'));
mkdirSync(outDir, { recursive: true });

const indexPath = join(audioDir, 'index.json');
const clips = existsSync(indexPath) ? JSON.parse(readFileSync(indexPath, 'utf8')) : {};
if (!Object.keys(clips).length) console.warn('No clips in tour-audio: run narrate.mjs first for a narrated video.');

const browser = await chromium.launch();
const context = await browser.newContext({ viewport: { width, height }, recordVideo: { dir: outDir, size: { width, height } } });
const page = await context.newPage();
// The recording starts with the page, so the moments the page reports are measured from here.
const started = Date.now();
await page.goto(`${url}/?video#Tour`);
await page.waitForFunction(() => window.tourVideo?.ready(), null, { timeout: 60000 });
console.log(`Playing the tour (${readTour().sections?.length ?? 0} sections)…`);
const played = await page.evaluate(() => window.tourVideo.play());
const video = page.video();
await context.close();
await browser.close();
const recording = await video.path();
console.log(`Recorded ${recording}: ${played.cues.length} texts, ${played.chapters.length} chapters, ${Math.round((played.ended - played.started) / 1000)}s`);

// Each clip starts where the page began reading its text, one at a time; the chapters become the file's chapters.
const inputs = [], filters = [], mixed = [];
for (const [i, cue] of played.cues.entries()) {
  const clip = clips[cue.key];
  if (!clip) continue;
  const offset = Math.max(0, cue.at - started);
  inputs.push('-i', join(audioDir, clip.file));
  filters.push(`[${inputs.length / 2}:a]adelay=${offset}|${offset}[a${i}]`);
  mixed.push(`[a${i}]`);
}
const meta = [';FFMETADATA1', ...played.chapters.map((c, i) => {
  const from = Math.max(0, c.at - started), to = played.chapters[i + 1] ? played.chapters[i + 1].at - started : played.ended - started;
  return `[CHAPTER]\nTIMEBASE=1/1000\nSTART=${from}\nEND=${to}\ntitle=${c.name.replace(/[=;#\\]/g, ' ')}`;
})].join('\n');
const metaPath = join(outDir, 'chapters.ffmeta');
writeFileSync(metaPath, meta + '\n');

const args = ['-y', '-i', recording, ...inputs, '-i', metaPath, '-map_metadata', String(inputs.length / 2 + 1)];
if (mixed.length) args.push('-filter_complex', `${filters.join(';')};${mixed.join('')}amix=inputs=${mixed.length}:normalize=0:dropout_transition=0[a]`, '-map', '0:v', '-map', '[a]', '-c:a', 'aac');
else args.push('-map', '0:v');
args.push('-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', out);
const ffmpeg = spawnSync('ffmpeg', args, { stdio: ['ignore', 'ignore', 'pipe'] });
if (ffmpeg.status !== 0) {
  console.error(ffmpeg.error?.message ?? ffmpeg.stderr.toString().split('\n').slice(-12).join('\n'));
  console.error(`ffmpeg failed; the silent recording is at ${recording}`);
  process.exit(1);
}
console.log(`Wrote ${out}`);
