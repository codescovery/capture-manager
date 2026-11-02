import { describe, expect, it } from 'vitest';
import { findSource, handleFromLabel, parseBridgeMessage, type MatchableSource } from './focus';

const sources: MatchableSource[] = [
  { id: 1, label: 'window:1180:0', name: 'Notepad' },
  { id: 2, label: 'window:2200:0', name: 'Visual Studio' },
];

describe('parseBridgeMessage', () => {
  it('reads a blur message', () => {
    expect(parseBridgeMessage('{"focused":false}')).toEqual({ focused: false });
  });

  it('reads a focus message', () => {
    const focus = parseBridgeMessage('{"focused":true,"focusedTitle":"a.txt - Notepad","processName":"notepad","handle":1180}');
    expect(focus).toEqual({ focused: true, focusedTitle: 'a.txt - Notepad', processName: 'notepad', handle: 1180 });
  });

  it('accepts the legacy message without focused flag', () => {
    expect(parseBridgeMessage('{"focusedTitle":"Doc"}')?.focused).toBe(true);
  });

  it('rejects garbage', () => {
    expect(parseBridgeMessage('nope')).toBeNull();
    expect(parseBridgeMessage('42')).toBeNull();
  });
});

describe('handleFromLabel', () => {
  it('extracts the native window id', () => {
    expect(handleFromLabel('window:1180:0')).toBe(1180);
  });

  it('returns null for other labels', () => {
    expect(handleFromLabel('screen:0:0')).toBeNull();
  });
});

describe('findSource', () => {
  it('prefers the window handle', () => {
    const found = findSource(sources, { focused: true, focusedTitle: 'unrelated', handle: 2200 });
    expect(found?.id).toBe(2);
  });

  it('falls back to the user given name', () => {
    const found = findSource(sources, { focused: true, focusedTitle: 'notes.txt - Notepad' });
    expect(found?.id).toBe(1);
  });

  it('finds nothing for an unknown window', () => {
    expect(findSource(sources, { focused: true, focusedTitle: 'Calculator', handle: 9 })).toBeUndefined();
  });

  it('finds nothing when blurred', () => {
    expect(findSource(sources, { focused: false })).toBeUndefined();
  });
});
