# Database Schema and Safe Use

For isolated empty development databases, the execution order is 001_schema.sql, 002_integrity.sql, then 003_seed_reference.sql. These are validated baseline design scripts, not authorization to modify an existing database. Reviewed EF Core migrations are the implementation deployment mechanism. 004_permissions.example.sql is a role/privilege example requiring separate role provisioning; it creates no production password.

The 77 tables share the blueprint-model.json source. documents is the only canonical header. stock_documents, production_orders, journal_entries, financial_documents, expenses, purchase_documents, sales_documents and harvest_receipts use the same id as a primary/foreign key to documents. API identifiers are documents.id; there is no competing typed-document id or version.

Opening stock uses movementType=OPENING rather than a parallel opening ledger. agri_expense_links refers to the same finance expense; it does not duplicate a cash amount. document_effective_status derives REVERSED from document_reversals while the original remains POSTED. Posted header/line triggers, ledger/audit append-only controls and restricted foreign keys preserve history.

Lot identity is mandatory internally. Untracked items receive one system lot. Composite lot/item constraints prevent mismatch. Items with unit_status=PENDING cannot post. Mass uses KG. Packaging stays pending until its approved unit model exists; do not invent a bag weight.

version is a positive application-incremented bigint and a JSON string. Configure IsConcurrencyToken() in EF Core rather than SQL Server rowversion. Reversals and settlements add new records instead of editing posted history. Atomic posting, lock ordering and idempotency still require Application implementation in addition to the SQL integrity barriers.

Fixed constraints reject a sixteenth account, a fifth warehouse and more than three remote-enabled users. Reference seed provisions 15 inactive slots with no real passwords, no automatic administrator and no enabled remote access. Secure initial provisioning is a separate documented operator procedure. VPN enrollment alone does not replace user/device/scope/MFA checks.

The validation results demonstrate isolated PostgreSQL SQL execution and selected integrity rules. They do not prove Windows Server configuration, EF migration deployment, real lock behavior under production load, VPN enrollment or actual backup restoration. No existing disks or production databases are changed.
