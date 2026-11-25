/// Bytes per binary gigabyte — the same convention as the purchase UI's "GB".
export const BYTES_PER_GIGABYTE = 1024 ** 3;

const capacity_formatter = new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 1 });

const capacity_units: readonly (readonly [bytes: number, label: string])[] = [
  [BYTES_PER_GIGABYTE * 1024, 'TB'],
  [BYTES_PER_GIGABYTE, 'GB'],
  [1024 ** 2, 'MB'],
  [1024, 'KB'],
];

/// Deliberately unrounded: an edit form pre-filled from it must give back the
/// exact stored bytes through `gigabytesToBytes`.
export function bytesToGigabytes(bytes: number): number {
  return bytes / BYTES_PER_GIGABYTE;
}

export function gigabytesToBytes(gigabytes?: number): number {
  const value = Number.isFinite(gigabytes) ? gigabytes! : 0;

  return Math.round(value * BYTES_PER_GIGABYTE);
}

export function printCapacity(bytes?: number): string {
  const value = Number.isFinite(bytes) ? Math.max(bytes!, 0) : 0;

  for (const [unit_bytes, label] of capacity_units) {
    if (value >= unit_bytes) return `${capacity_formatter.format(value / unit_bytes)} ${label}`;
  }

  return `${capacity_formatter.format(value)} B`;
}
