// Reads every text of the tour aloud (see narration.mjs) from the command line: the tour comes from the local server when
// one is running, else from tour.json.
// Usage: node narrate.mjs [--voice af_heart] [--force] [--url http://localhost:5178]
import { arg, audioDir, readTour } from './shared.mjs';
import { narrate } from './narration.mjs';

const started = Date.now();
const { clips, read } = await narrate(await readTour(), { voice: arg('voice', 'af_heart'), force: process.argv.includes('--force'), log: console.log });
console.log(`${clips} clips in ${audioDir}, ${read} sentences read in ${Math.round((Date.now() - started) / 1000)}s`);
