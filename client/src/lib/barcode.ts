// Barcode rules (assumption A13). MUST match the server's Barcode.Pattern in src/Ims.Api/Domain/Barcode.cs;
// a test on each side pins the pattern so they cannot drift apart.
export const BARCODE_PATTERN = '^[A-Z0-9-]{6,32}$'
const BARCODE_REGEX = new RegExp(BARCODE_PATTERN)

/** Trim and upper-case what the user typed or the scanner sent. */
export function normalizeBarcode(raw: string | null | undefined): string {
  return (raw ?? '').trim().toUpperCase()
}

/** True when an already-normalized barcode has a valid shape. */
export function isValidBarcode(normalized: string): boolean {
  return BARCODE_REGEX.test(normalized)
}
