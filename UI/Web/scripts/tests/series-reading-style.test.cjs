const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const typescript = require('typescript');

// Load the actual TypeScript helpers using the project's compiler, without an Angular test runner.
require.extensions['.ts'] = (module, filename) => {
  const source = fs.readFileSync(filename, 'utf8');
  const result = typescript.transpileModule(source, {compilerOptions: {module: typescript.ModuleKind.CommonJS}});
  module._compile(result.outputText, filename);
};

const {SeriesReadingStyle, initialSeriesReaderMode, readerModeForProfile} = require('../../src/app/_models/series-reading-style.ts');
const {ReaderMode} = require('../../src/app/_models/preferences/reader-mode.ts');

test('series style selects the starting reader without modifying profile mode', () => {
  assert.equal(initialSeriesReaderMode(SeriesReadingStyle.Manhwa, ReaderMode.LeftRight), ReaderMode.Webtoon);
  assert.equal(initialSeriesReaderMode(SeriesReadingStyle.Manga, ReaderMode.Webtoon), ReaderMode.LeftRight);
  assert.equal(initialSeriesReaderMode(SeriesReadingStyle.FollowProfile, ReaderMode.UpDown), ReaderMode.UpDown);
  assert.equal(readerModeForProfile(SeriesReadingStyle.Manhwa, ReaderMode.Webtoon, ReaderMode.UpDown), ReaderMode.UpDown);
  assert.equal(readerModeForProfile(SeriesReadingStyle.FollowProfile, ReaderMode.Webtoon, ReaderMode.UpDown), ReaderMode.Webtoon);
});
