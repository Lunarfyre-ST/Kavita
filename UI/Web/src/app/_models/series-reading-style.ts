import {ReaderMode} from './preferences/reader-mode';

export enum SeriesReadingStyle {
  FollowProfile = 0,
  Manga = 1,
  Manhwa = 2,
}

export function initialSeriesReaderMode(style: SeriesReadingStyle, profileMode: ReaderMode): ReaderMode {
  if (style === SeriesReadingStyle.Manhwa) return ReaderMode.Webtoon;
  if (style === SeriesReadingStyle.Manga) return ReaderMode.LeftRight;
  return profileMode;
}

export function readerModeForProfile(style: SeriesReadingStyle, currentMode: ReaderMode, profileMode: ReaderMode): ReaderMode {
  return style === SeriesReadingStyle.FollowProfile ? currentMode : profileMode;
}
