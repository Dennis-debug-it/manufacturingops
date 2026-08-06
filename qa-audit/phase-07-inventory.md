# Phase 7 — Inventory / Materials

Source-focused review + endpoint checks, 2026-08-02.

## Positives
- Negative-stock guard present: `IssueAsync` throws `BusinessRuleException("Insufficient stock for store item {Sku}.")` when `QuantityOnHand < RequestedQuantity`.
- Request → Approve → Issue → Use → Return workflow with status gating and audit logging.
- Return path clamps to `Math.Max(Issued - Used, 0)` (no negative returns).

## Findings
### F7-01 — HIGH — Race condition in material issue allows oversold / negative stock
- **Category**: Concurrency / Data integrity / Business logic
- **Location**: `ServiceOpsWorkflowServices.IssueAsync` (src/Ecosys.Infrastructure/Services/ServiceOpsWorkflowServices.cs:58-71)
- **Root cause**: read `QuantityOnHand` → check → `QuantityOnHand -= qty` → `SaveChanges`, with **no optimistic concurrency token** (StoreItem has no rowversion; no global `UseXminAsConcurrencyToken`) and **no serializable transaction**. Classic TOCTOU lost-update.
- **Failure scenario**: StoreItem qty=10. Two approved requests each for 8 are issued concurrently. Both read 10, both pass the `< RequestedQuantity` check, both decrement → final qty = -6. Stock oversold; inventory integrity broken.
- **Fix**: Add xmin/rowversion concurrency token to StoreItem (retry on `DbUpdateConcurrencyException`), OR perform an atomic guarded decrement (`UPDATE store_items SET quantity_on_hand = quantity_on_hand - @q WHERE id=@id AND quantity_on_hand >= @q` and treat 0 rows affected as insufficient stock), OR wrap issue in a serializable transaction.

## Not exhaustively tested
- Supplier CRUD, stock adjustments audit trail, reorder/low-stock alert thresholds, multi-branch stock isolation.
