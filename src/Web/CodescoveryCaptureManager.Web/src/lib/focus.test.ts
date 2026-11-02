import { describe, expect, it } from 'vitest';
import { parseBridgeMessage } from './focus';

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
