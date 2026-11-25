import {
  BYTES_PER_GIGABYTE,
  bytesToGigabytes,
  gigabytesToBytes,
  printCapacity,
} from './capacity-printer';

describe('capacity-printer (Docs/24 D3)', () => {
  const fa = new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 1 });

  it('uses the binary gigabyte (1024³) — the same unit as the purchase UI', () => {
    expect(BYTES_PER_GIGABYTE).toBe(1_073_741_824);
  });

  it('gigabytesToBytes converts whole and fractional inputs exactly', () => {
    expect(gigabytesToBytes(1)).toBe(1_073_741_824);
    expect(gigabytesToBytes(200)).toBe(214_748_364_800);
    expect(gigabytesToBytes(1.5)).toBe(1_610_612_736);
  });

  it('gigabytesToBytes rounds instead of truncating and treats missing input as zero', () => {
    // 0.7 GB = 751619276.8 bytes
    expect(gigabytesToBytes(0.7)).toBe(751_619_277);
    expect(gigabytesToBytes(undefined)).toBe(0);
    expect(gigabytesToBytes(Number.NaN)).toBe(0);
  });

  it('bytesToGigabytes keeps the stored bytes intact across a round trip', () => {
    // the edit form is pre-filled without rounding: an unrelated edit must not
    // change the stored figure
    for (const bytes of [1, 110, 1_073_741_824, 1_610_612_737, 214_748_364_801, 2 ** 50]) {
      expect(gigabytesToBytes(bytesToGigabytes(bytes))).toBe(bytes);
    }

    expect(bytesToGigabytes(200 * BYTES_PER_GIGABYTE)).toBe(200);
  });

  it('printCapacity picks the largest unit of the 1024 ladder', () => {
    expect(printCapacity(512)).toBe(`${fa.format(512)} B`);
    expect(printCapacity(1024)).toBe(`${fa.format(1)} KB`);
    expect(printCapacity(5 * 1024 ** 2)).toBe(`${fa.format(5)} MB`);
    expect(printCapacity(200 * BYTES_PER_GIGABYTE)).toBe(`${fa.format(200)} GB`);
    expect(printCapacity(2 * BYTES_PER_GIGABYTE * 1024)).toBe(`${fa.format(2)} TB`);
  });

  it('printCapacity keeps at most one decimal and drops it for whole numbers', () => {
    expect(printCapacity(1.5 * BYTES_PER_GIGABYTE)).toBe(`${fa.format(1.5)} GB`);
    expect(printCapacity(1024 ** 2 + 512 * 1024)).toBe(`${fa.format(1.5)} MB`);
  });

  it('printCapacity renders zero for undefined, NaN and negative values', () => {
    expect(printCapacity(undefined)).toBe(`${fa.format(0)} B`);
    expect(printCapacity(Number.NaN)).toBe(`${fa.format(0)} B`);
    expect(printCapacity(-5)).toBe(`${fa.format(0)} B`);
  });
});
