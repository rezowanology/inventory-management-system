// Client-side duplicate detection for the Track screen (assumption A8). The server re-checks everything;
// this only gives instant feedback while a scanner is beeping through a batch.

export type BatchCheck = { ok: true } | { ok: false; reason: string }

/**
 * Decides whether a scanned component barcode may be added to the batch of the unit being tracked.
 * Both inputs are normalized barcodes (trimmed + upper-case). A refusal is a warning, not a failure.
 */
export function checkComponentScan(
  componentBarcode: string,
  unitBarcode: string,
  alreadyInBatch: readonly string[],
): BatchCheck {
  if (componentBarcode === unitBarcode)
    return { ok: false, reason: 'Duplicate scan: this is the product barcode, not a component barcode.' }
  if (alreadyInBatch.includes(componentBarcode))
    return { ok: false, reason: `Duplicate scan: ${componentBarcode} is already in this batch.` }
  return { ok: true }
}
