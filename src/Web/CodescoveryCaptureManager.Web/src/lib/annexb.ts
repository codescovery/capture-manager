const SPS_NAL_TYPE = 7;

/** Finds the next Annex-B start code (00 00 01) at or after `from`; returns its index or -1. */
function findStartCode(data: Uint8Array, from: number): number {
  for (let index = from; index + 2 < data.length; index++) {
    if (data[index] === 0 && data[index + 1] === 0 && data[index + 2] === 1) return index;
  }
  return -1;
}

/** Returns the first NAL unit of `type` (without start code), or null. */
export function findNal(data: Uint8Array, type: number): Uint8Array | null {
  let start = findStartCode(data, 0);
  while (start !== -1) {
    const payload = start + 3;
    const next = findStartCode(data, payload);
    const end = next === -1 ? data.length : trimTrailingZeros(data, payload, next);
    if (payload < data.length && (data[payload] & 0x1f) === type) return data.subarray(payload, end);
    start = next;
  }
  return null;
}

function trimTrailingZeros(data: Uint8Array, from: number, to: number): number {
  let end = to;
  while (end > from && data[end - 1] === 0) end--;
  return end;
}

export function findSps(data: Uint8Array): Uint8Array | null {
  return findNal(data, SPS_NAL_TYPE);
}

/** WebCodecs codec string (`avc1.PPCCLL`) from the SPS profile, constraint and level bytes. */
export function codecFromSps(sps: Uint8Array): string | null {
  if (sps.length < 4) return null;
  const hex = (value: number) => value.toString(16).padStart(2, '0');
  return `avc1.${hex(sps[1])}${hex(sps[2])}${hex(sps[3])}`;
}

export function sameBytes(left: Uint8Array | null, right: Uint8Array | null): boolean {
  if (!left || !right || left.length !== right.length) return false;
  return left.every((value, index) => value === right[index]);
}
