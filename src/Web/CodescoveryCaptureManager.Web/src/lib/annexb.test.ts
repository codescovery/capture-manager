import { describe, expect, it } from 'vitest';
import { codecFromSps, findSps, sameBytes } from './annexb';

// 4-byte start code + SPS (High profile, level 4.0) + 3-byte start code + PPS + IDR.
const accessUnit = new Uint8Array([
  0, 0, 0, 1, 0x67, 0x64, 0x00, 0x28, 0xac, 0xd9,
  0, 0, 1, 0x68, 0xee, 0x3c, 0x80,
  0, 0, 1, 0x65, 0x88, 0x84,
]);

describe('annexb', () => {
  it('finds the SPS without start code or padding', () => {
    expect(Array.from(findSps(accessUnit)!)).toEqual([0x67, 0x64, 0x00, 0x28, 0xac, 0xd9]);
  });

  it('builds the codec string', () => {
    expect(codecFromSps(findSps(accessUnit)!)).toBe('avc1.640028');
  });

  it('returns null without an SPS', () => {
    expect(findSps(new Uint8Array([0, 0, 1, 0x65, 1, 2]))).toBeNull();
  });

  it('compares bytes', () => {
    expect(sameBytes(new Uint8Array([1, 2]), new Uint8Array([1, 2]))).toBe(true);
    expect(sameBytes(new Uint8Array([1, 2]), new Uint8Array([1, 3]))).toBe(false);
    expect(sameBytes(null, new Uint8Array([1]))).toBe(false);
  });
});
