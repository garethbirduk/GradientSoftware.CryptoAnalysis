// A narration worker the local server keeps running, so the model is loaded once rather than on every run: each line on
// stdin is a request, { "tour": <the tour as the page plays it>, "voice": "af_heart" }, and the worker answers on stdout
// with a line per text read, { "read": "<key> <seconds>s <text>" }, then { "done": true, "clips": n, "read": m } or
// { "error": "..." }. Requests are taken one at a time, in order.
import { createInterface } from 'node:readline';
import { narrate } from './narration.mjs';

const say = line => process.stdout.write(JSON.stringify(line) + '\n');
let queue = Promise.resolve();

createInterface({ input: process.stdin }).on('line', line => {
  if (!line.trim()) return;
  queue = queue.then(async () => {
    try {
      const request = JSON.parse(line);
      const result = await narrate(request.tour, { voice: request.voice ?? 'af_heart', force: request.force === true, log: read => say({ read }) });
      say({ done: true, ...result });
    } catch (e) {
      say({ error: e?.message ?? String(e) });
    }
  });
});

process.stdin.on('end', () => process.exit(0));
