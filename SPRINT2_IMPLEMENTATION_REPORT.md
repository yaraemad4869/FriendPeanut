# Sprint 2 Implementation Report

## Sprint

S2 - Stock Ledger and Atomic Posting

## Planned Features

- INV-01 Opening stock balances
- INV-02 Purchase receipt
- INV-03 Harvest receipt
- INV-04 Production receipt
- INV-05 Production material issue
- INV-06 Agricultural input issue
- INV-07 Sales stock issue
- INV-12 Intermediate-product stock movement
- INV-14 Document approval and posting lifecycle
- INV-15 Stock availability and negative-stock exception
- INV-16 Idempotent posting
- INV-17 Current stock balances
- INV-18 Item movement ledger

## Engineering Rules

- PostgreSQL transaction boundaries
- Append-only stock ledger
- Stock balance cache updated atomically
- Deterministic lock ordering
- Exact numeric(18,3) quantities
- Idempotent command handling
- Optimistic concurrency
- Authorization and resource scope enforcement
- Audit trail for posting
- No direct balance-edit endpoint
- No modification of posted documents
- No automatic approval of pending policies

## Remaining Integration Work

The repository adapter must be completed against the approved database schema.
The API must be registered in the existing endpoint composition.
Idempotency reservation and response replay must use the existing shared abstraction.
Negative-stock authorization must be checked by the current-user permission service.
All database operations must execute within one unit of work and transaction.

## Acceptance

Sprint 2 is not considered complete until:

- PostgreSQL integration tests pass.
- Posting is atomic under concurrent requests.
- Replayed idempotency keys do not duplicate ledger entries.
- Ledger and balance cache reconcile.
- Scope and permission tests pass.
- Contract and migration review is complete.
- Independent peer review is recorded.
