# Tour video

Turns the site's tour (`CryptoAnalysis.Site/wwwroot/tour.json`) into a narrated MP4 with chapters.

1. `npm install` here once, then `npx playwright install chromium` for the browser it records with. ffmpeg must be on the path.
2. Start the local server from the repository root: `./build-site.ps1 -Serve`.
3. `npm run narrate` reads every text with Kokoro into `wwwroot/tour-audio` (the model is downloaded on first use). Texts already read are skipped; `--voice bf_emma` picks another voice, `--force` reads everything again. The texts come from the server (`--url` points it at another), which writes out the sections that explain something from the prices; without it, those texts are not read. With the clips in place, the Tour page uses each clip's length as its text's seconds and its Voice button plays them.
4. `npm run record` plays the tour in a headless browser, records it, lays the clips over it and writes `artifacts/tour-video/tour.mp4`. `--url` points it at another server, `--width` and `--height` set the frame.

`npm run video` does 3 and 4 together. The Tour page does the same through the local server: Update audio is 3, which is all its Voice button needs, Generate video is 3 and 4, and Download video fetches the result.

What is read aloud can differ from what is shown:

- The tour's `glossary` gives what the voice says for a word or phrase, as in `"glossary": { "BoS": "Break of Structure" }`. An entry stands for a whole word in the case it is written in, and the longest entry that fits is used. Texts whose spoken words change are read again on the next run.
- A text with `"voice": false` (Silent in the editor) is shown but not read.
