# Data Dictionary — 77 Tables

PostgreSQL 17. This dictionary uses the same unchanged model as the SQL/ERD. SQL scripts are authoritative for triggers and procedural integrity; check 002_integrity.sql as well as the table definitions. Numeric/version strings apply at the JSON boundary.

## app_users

Group: Identity. 15 preallocated slots; enabling remote access is separate from roles.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| slot_no | smallint | No | None |  |
| username | varchar(80) | No | None |  |
| display_name | varchar(160) | No | None |  |
| password_hash | varchar(512) | Yes | None |  |
| status | varchar(40) | No | 'INACTIVE' | ; values: INACTIVE, ACTIVE, LOCKED |
| must_change_password | boolean | No | true |  |
| remote_access_enabled | boolean | No | false |  |
| failed_login_count | integer | No | 0 |  |
| security_stamp | uuid | No | gen_random_uuid() |  |
| version | bigint | No | 1 |  |
| created_at | timestamptz | No | now() |  |

Unique constraints: `slot_no`; `username`

Checks: `slot_no BETWEEN 1 AND 15`; `failed_login_count >= 0`; `status <> 'ACTIVE' OR password_hash IS NOT NULL`

## roles

Group: Identity. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(60) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## permissions

Group: Identity. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(100) | No | None |  |
| description_ar | varchar(240) | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## user_roles

Group: Identity. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| user_id | uuid | No | None | FK -> app_users.id |
| role_id | uuid | No | None | FK -> roles.id |
| version | bigint | No | 1 |  |

Unique constraints: `user_id, role_id`

Checks: No additional table CHECK recorded.

## role_permissions

Group: Identity. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| role_id | uuid | No | None | FK -> roles.id |
| permission_id | uuid | No | None | FK -> permissions.id |
| version | bigint | No | 1 |  |

Unique constraints: `role_id, permission_id`

Checks: No additional table CHECK recorded.

## user_scopes

Group: Identity. No NULL-means-all interpretation. Global scope is an explicit permission policy.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| user_id | uuid | No | None | FK -> app_users.id |
| scope_kind | varchar(40) | No | None | ; values: WAREHOUSE, FACTORY, DEPARTMENT |
| warehouse_id | uuid | Yes | None | FK -> warehouses.id |
| factory_id | uuid | Yes | None | FK -> factories.id |
| department_code | varchar(60) | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `(scope_kind='WAREHOUSE' AND warehouse_id IS NOT NULL AND factory_id IS NULL AND department_code IS NULL) OR (scope_kind='FACTORY' AND factory_id IS NOT NULL AND warehouse_id IS NULL AND department_code IS NULL) OR (scope_kind='DEPARTMENT' AND department_code IS NOT NULL AND warehouse_id IS NULL AND factory_id IS NULL)`

## registered_devices

Group: Identity. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| external_device_key | varchar(200) | No | None |  |
| display_name | varchar(160) | No | None |  |
| status | varchar(40) | No | 'PENDING' | ; values: PENDING, APPROVED, REVOKED |
| approved_by | uuid | Yes | None | FK -> app_users.id |
| approved_at | timestamptz | Yes | None |  |
| revoked_at | timestamptz | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `external_device_key`

Checks: No additional table CHECK recorded.

## user_devices

Group: Identity. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| user_id | uuid | No | None | FK -> app_users.id |
| device_id | uuid | No | None | FK -> registered_devices.id |
| remote_allowed | boolean | No | false |  |
| created_at | timestamptz | No | now() |  |
| version | bigint | No | 1 |  |

Unique constraints: `user_id, device_id`

Checks: No additional table CHECK recorded.

## auth_sessions

Group: Identity. Cookie contains an opaque token; only hash stored. Device identity comes from a trusted gateway.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| user_id | uuid | No | None | FK -> app_users.id |
| token_hash | varchar(128) | No | None |  |
| device_id | uuid | Yes | None | FK -> registered_devices.id |
| access_channel | varchar(40) | No | None | ; values: LAN, VPN |
| created_at | timestamptz | No | now() |  |
| expires_at | timestamptz | No | None |  |
| revoked_at | timestamptz | Yes | None |  |
| mfa_verified_at | timestamptz | Yes | None |  |
| security_stamp | uuid | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `token_hash`

Checks: `expires_at > created_at`

## auth_challenges

Group: Identity. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| user_id | uuid | No | None | FK -> app_users.id |
| challenge_hash | varchar(128) | No | None |  |
| next_step | varchar(40) | No | None | ; values: CHANGE_PASSWORD, MFA_SETUP, MFA_VERIFY |
| expires_at | timestamptz | No | None |  |
| consumed_at | timestamptz | Yes | None |  |
| attempts | integer | No | 0 |  |
| version | bigint | No | 1 |  |

Unique constraints: `challenge_hash`

Checks: `attempts >= 0`

## mfa_credentials

Group: Identity. Encrypted TOTP secret, never in logs, exports or user DTOs.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| user_id | uuid | No | None | FK -> app_users.id |
| secret_ciphertext | varchar(1024) | No | None |  |
| encryption_key_id | varchar(100) | No | None |  |
| is_confirmed | boolean | No | false |  |
| last_accepted_time_step | bigint | Yes | None |  |
| confirmed_at | timestamptz | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `user_id`

Checks: No additional table CHECK recorded.

## mfa_recovery_codes

Group: Identity. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| user_id | uuid | No | None | FK -> app_users.id |
| code_hash | varchar(128) | No | None |  |
| used_at | timestamptz | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `code_hash`

Checks: No additional table CHECK recorded.

## login_attempts

Group: Identity. Restricted security metadata with approved retention; no password, OTP or token.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| user_id | uuid | Yes | None | FK -> app_users.id |
| occurred_at | timestamptz | No | now() |  |
| succeeded | boolean | No | false |  |
| failure_code | varchar(80) | Yes | None |  |
| source_ip | inet | Yes | None |  |
| correlation_id | varchar(80) | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: No additional table CHECK recorded.

## factories

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(20) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: `code IN ('F1_SORTING','F2_PEELING')`

## sites

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(30) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| is_active | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## warehouses

Group: MasterData. Exactly four seeded warehouses, no create/delete API.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(30) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| site_id | uuid | Yes | None | FK -> sites.id |
| factory_id | uuid | Yes | None | FK -> factories.id |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: `code IN ('WH_AGRI','WH_SORTED','WH_PEELED','WH_PACKAGING')`

## item_categories

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(40) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| parent_id | uuid | Yes | None | FK -> item_categories.id |
| is_active | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## units

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(20) | No | None |  |
| name_ar | varchar(80) | No | None |  |
| dimension | varchar(40) | No | None | ; values: MASS, COUNT, VOLUME, LENGTH |
| is_active | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## items

Group: MasterData. Mass items use KG base. Non-mass units remain blocked until approved; no fabricated bag weight.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(50) | No | None |  |
| name_ar | varchar(200) | No | None |  |
| normalized_name | varchar(200) | No | None |  |
| category_id | uuid | No | None | FK -> item_categories.id |
| base_unit_id | uuid | Yes | None | FK -> units.id |
| unit_status | varchar(40) | No | 'PENDING' | ; values: PENDING, APPROVED |
| lot_tracking_enabled | boolean | No | true |  |
| can_purchase | boolean | No | false |  |
| can_sell | boolean | No | false |  |
| can_manufacture | boolean | No | false |  |
| is_active | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: `unit_status <> 'APPROVED' OR base_unit_id IS NOT NULL`

## item_aliases

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| item_id | uuid | No | None | FK -> items.id |
| alias_ar | varchar(200) | No | None |  |
| normalized_alias | varchar(200) | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `normalized_alias`

Checks: No additional table CHECK recorded.

## unit_conversions

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| item_id | uuid | No | None | FK -> items.id |
| from_unit_id | uuid | No | None | FK -> units.id |
| to_unit_id | uuid | No | None | FK -> units.id |
| factor | numeric(18,6) | No | None |  |
| valid_from | date | No | None |  |
| valid_to | date | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `item_id, from_unit_id, to_unit_id, valid_from`

Checks: `factor > 0`; `from_unit_id <> to_unit_id`; `valid_to IS NULL OR valid_to >= valid_from`

## lots

Group: MasterData. Untracked items resolve to one system lot. Ledger lot_id is never NULL.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| item_id | uuid | No | None | FK -> items.id |
| code | varchar(60) | No | None |  |
| is_system_untracked | boolean | No | false |  |
| received_date | date | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `item_id, code`; `id, item_id`

Checks: No additional table CHECK recorded.

## item_warehouse_rules

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| item_id | uuid | No | None | FK -> items.id |
| warehouse_id | uuid | No | None | FK -> warehouses.id |
| is_allowed | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `item_id, warehouse_id`

Checks: No additional table CHECK recorded.

## parties

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(50) | No | None |  |
| name_ar | varchar(200) | No | None |  |
| phone | varchar(40) | Yes | None |  |
| address | varchar(400) | Yes | None |  |
| tax_number | varchar(60) | Yes | None |  |
| is_active | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## party_roles

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| party_id | uuid | No | None | FK -> parties.id |
| role_code | varchar(40) | No | None | ; values: CUSTOMER, SUPPLIER, CONTRACTOR |
| version | bigint | No | 1 |  |

Unique constraints: `party_id, role_code`

Checks: No additional table CHECK recorded.

## party_contacts

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| party_id | uuid | No | None | FK -> parties.id |
| display_name | varchar(160) | No | None |  |
| phone | varchar(40) | Yes | None |  |
| email | varchar(200) | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: No additional table CHECK recorded.

## cost_centers

Group: MasterData. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(40) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| parent_id | uuid | Yes | None | FK -> cost_centers.id |
| is_active | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## documents

Group: Documents. Posted rows immutable. REVERSED is derived via document_reversals, not a header update.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| document_no | varchar(60) | No | None |  |
| document_type | varchar(40) | No | None | ; values: STOCK, PRODUCTION, JOURNAL, FINANCIAL, PURCHASE, SALE, EXPENSE, HARVEST |
| document_date | date | No | None |  |
| status | varchar(40) | No | 'DRAFT' | ; values: DRAFT, SUBMITTED, APPROVED, POSTED, REJECTED, CANCELLED |
| created_by | uuid | No | None | FK -> app_users.id |
| created_at | timestamptz | No | now() |  |
| approved_by | uuid | Yes | None | FK -> app_users.id |
| approved_at | timestamptz | Yes | None |  |
| posted_by | uuid | Yes | None | FK -> app_users.id |
| posted_at | timestamptz | Yes | None |  |
| reason | varchar(600) | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `document_no`

Checks: `status <> 'POSTED' OR (posted_at IS NOT NULL AND posted_by IS NOT NULL)`

## document_sequences

Group: Documents. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| sequence_key | varchar(40) | No | None |  |
| year | smallint | No | None |  |
| last_number | bigint | No | 0 |  |
| version | bigint | No | 1 |  |

Unique constraints: `sequence_key, year`

Checks: `last_number >= 0`

## document_reversals

Group: Documents. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| original_document_id | uuid | No | None | FK -> documents.id |
| reversal_document_id | uuid | No | None | FK -> documents.id |
| reason | varchar(600) | No | None |  |
| created_by | uuid | No | None | FK -> app_users.id |
| created_at | timestamptz | No | now() |  |

Unique constraints: `original_document_id`; `reversal_document_id`

Checks: `original_document_id <> reversal_document_id`

## document_attachments

Group: Documents. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| document_id | uuid | No | None | FK -> documents.id |
| storage_key | varchar(240) | No | None |  |
| original_name | varchar(200) | No | None |  |
| content_type | varchar(100) | No | None |  |
| byte_count | bigint | No | None |  |
| sha256 | varchar(64) | No | None |  |
| uploaded_by | uuid | No | None | FK -> app_users.id |
| created_at | timestamptz | No | now() |  |
| version | bigint | No | 1 |  |

Unique constraints: `storage_key`

Checks: `byte_count > 0`

## idempotency_records

Group: System. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| user_id | uuid | No | None | FK -> app_users.id |
| route_key | varchar(200) | No | None |  |
| idempotency_key | varchar(100) | No | None |  |
| request_hash | varchar(64) | No | None |  |
| state | varchar(40) | No | 'IN_PROGRESS' | ; values: IN_PROGRESS, COMPLETED |
| response_status | integer | Yes | None |  |
| response_body | jsonb | Yes | None |  |
| expires_at | timestamptz | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `user_id, route_key, idempotency_key`

Checks: No additional table CHECK recorded.

## audit_events

Group: System. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| actor_id | uuid | Yes | None | FK -> app_users.id |
| action | varchar(100) | No | None |  |
| entity_type | varchar(100) | No | None |  |
| entity_id | uuid | Yes | None |  |
| before_redacted | jsonb | Yes | None |  |
| after_redacted | jsonb | Yes | None |  |
| reason | varchar(600) | Yes | None |  |
| correlation_id | varchar(80) | No | None |  |
| occurred_at | timestamptz | No | now() |  |

Unique constraints: None additional to the primary key.

Checks: No additional table CHECK recorded.

## policy_decisions

Group: System. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| policy_key | varchar(100) | No | None |  |
| status | varchar(40) | No | 'PENDING' | ; values: PENDING, APPROVED, RETIRED |
| version | bigint | No | 1 |  |
| approved_configuration | jsonb | Yes | None |  |
| approved_by | uuid | Yes | None | FK -> app_users.id |
| approved_at | timestamptz | Yes | None |  |

Unique constraints: `policy_key`

Checks: `status <> 'APPROVED' OR (approved_by IS NOT NULL AND approved_at IS NOT NULL)`

## settings

Group: System. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| key | varchar(100) | No | None |  |
| value | jsonb | No | None |  |
| policy_decision_id | uuid | Yes | None | FK -> policy_decisions.id |
| version | bigint | No | 1 |  |

Unique constraints: `key`

Checks: No additional table CHECK recorded.

## stock_documents

Group: Inventory.  Shared primary key: id is documents.id and is the canonical API document id.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | None | FK -> documents.id |
| movement_type | varchar(40) | No | None | ; values: OPENING, PURCHASE_RECEIPT, HARVEST_RECEIPT, PRODUCTION_ISSUE, PRODUCTION_RECEIPT, AGRI_ISSUE, SALES_ISSUE, TRANSFER, PURCHASE_RETURN, SALES_RETURN, ADJUSTMENT, INTERMEDIATE_RECEIPT, INTERMEDIATE_ISSUE, REVERSAL |
| warehouse_id | uuid | No | None | FK -> warehouses.id |
| counter_warehouse_id | uuid | Yes | None | FK -> warehouses.id |
| party_id | uuid | Yes | None | FK -> parties.id |
| party_snapshot | jsonb | Yes | None |  |
| source_document_id | uuid | Yes | None | FK -> documents.id |
| production_stage_id | uuid | Yes | None | FK -> production_stages.id |

Unique constraints: None additional to the primary key.

Checks: `(movement_type='TRANSFER' AND counter_warehouse_id IS NOT NULL AND warehouse_id<>counter_warehouse_id) OR (movement_type<>'TRANSFER' AND counter_warehouse_id IS NULL)`

## stock_document_lines

Group: Inventory. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| stock_document_id | uuid | No | None | FK -> stock_documents.id |
| line_no | integer | No | None |  |
| item_id | uuid | No | None | FK -> items.id |
| lot_id | uuid | No | None | FK -> lots.id |
| quantity_entered | numeric(18,3) | No | None |  |
| unit_id | uuid | No | None | FK -> units.id |
| conversion_factor | numeric(18,6) | No | None |  |
| quantity_base | numeric(18,3) | No | None |  |
| base_unit_id | uuid | No | None | FK -> units.id |
| direction | smallint | No | None |  |
| unit_price | numeric(18,4) | Yes | None |  |
| item_name_snapshot | varchar(200) | No | None |  |
| unit_code_snapshot | varchar(20) | No | None |  |
| description | varchar(400) | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `stock_document_id, line_no`

Checks: `quantity_entered > 0`; `conversion_factor > 0`; `quantity_base > 0`; `direction IN (-1,1)`; `unit_price IS NULL OR unit_price >= 0`

## posting_batches

Group: Inventory. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| document_id | uuid | No | None | FK -> documents.id |
| posted_by | uuid | No | None | FK -> app_users.id |
| posted_at | timestamptz | No | now() |  |
| engine_version | varchar(40) | No | None |  |

Unique constraints: `document_id`

Checks: No additional table CHECK recorded.

## stock_ledger_entries

Group: Inventory. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| posting_batch_id | uuid | No | None | FK -> posting_batches.id |
| stock_line_id | uuid | No | None | FK -> stock_document_lines.id |
| item_id | uuid | No | None | FK -> items.id |
| warehouse_id | uuid | No | None | FK -> warehouses.id |
| lot_id | uuid | No | None | FK -> lots.id |
| base_unit_id | uuid | No | None | FK -> units.id |
| quantity_delta | numeric(18,3) | No | None |  |
| unit_cost | numeric(18,4) | Yes | None |  |
| posted_at | timestamptz | No | now() |  |

Unique constraints: `posting_batch_id, stock_line_id, warehouse_id`

Checks: `quantity_delta <> 0`

## stock_balances

Group: Inventory. Derived cache. Application cannot use the flag without stock.allow_negative and an audited per-posting reason.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| item_id | uuid | No | None | FK -> items.id |
| warehouse_id | uuid | No | None | FK -> warehouses.id |
| lot_id | uuid | No | None | FK -> lots.id |
| base_unit_id | uuid | No | None | FK -> units.id |
| quantity_base | numeric(18,3) | No | 0 |  |
| negative_override_authorized | boolean | No | false |  |
| negative_override_reason | varchar(600) | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `item_id, warehouse_id, lot_id`

Checks: `quantity_base >= 0 OR (negative_override_authorized AND length(trim(coalesce(negative_override_reason,'')))>0)`

## stock_counts

Group: Inventory. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| count_no | varchar(60) | No | None |  |
| warehouse_id | uuid | No | None | FK -> warehouses.id |
| count_date | date | No | None |  |
| status | varchar(40) | No | 'DRAFT' | ; values: DRAFT, FINALIZED, CANCELLED |
| snapshot_at | timestamptz | No | None |  |
| adjustment_document_id | uuid | Yes | None | FK -> documents.id |
| version | bigint | No | 1 |  |

Unique constraints: `count_no`; `adjustment_document_id`

Checks: No additional table CHECK recorded.

## stock_count_lines

Group: Inventory. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| stock_count_id | uuid | No | None | FK -> stock_counts.id |
| item_id | uuid | No | None | FK -> items.id |
| lot_id | uuid | No | None | FK -> lots.id |
| snapshot_quantity | numeric(18,3) | No | None |  |
| counted_quantity | numeric(18,3) | No | None |  |
| reason | varchar(600) | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `stock_count_id, item_id, lot_id`

Checks: `counted_quantity >= 0`

## stock_reservations

Group: Inventory. Disabled future extension; no API/UI in the baseline.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| item_id | uuid | No | None | FK -> items.id |
| warehouse_id | uuid | No | None | FK -> warehouses.id |
| lot_id | uuid | No | None | FK -> lots.id |
| quantity_base | numeric(18,3) | No | None |  |
| enabled | boolean | No | false |  |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `quantity_base > 0`; `enabled = false`

## production_orders

Group: Manufacturing.  Shared primary key: id is documents.id and is the canonical API document id.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | None | FK -> documents.id |
| factory_id | uuid | No | None | FK -> factories.id |
| responsible_user_id | uuid | No | None | FK -> app_users.id |
| parent_order_id | uuid | Yes | None | FK -> production_orders.id |

Unique constraints: None additional to the primary key.

Checks: No additional table CHECK recorded.

## production_stages

Group: Manufacturing. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| order_id | uuid | No | None | FK -> production_orders.id |
| sequence_no | integer | No | None |  |
| stage_type | varchar(40) | No | None | ; values: SORTING, OVEN, PEELING, SECOND_SORT, REPROCESSING |
| nesba_path | varchar(40) | Yes | None | ; values: DIRECT, STORED |
| status | varchar(40) | No | 'DRAFT' | ; values: DRAFT, READY, POSTED |
| input_quantity | numeric(18,3) | No | 0 |  |
| output_quantity | numeric(18,3) | No | 0 |  |
| loss_quantity | numeric(18,3) | No | 0 |  |
| stock_document_id | uuid | Yes | None | FK -> documents.id |
| version | bigint | No | 1 |  |

Unique constraints: `order_id, sequence_no`; `stock_document_id`

Checks: `sequence_no > 0`; `input_quantity >= 0`; `output_quantity >= 0`; `loss_quantity >= 0`

## production_inputs

Group: Manufacturing. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| stage_id | uuid | No | None | FK -> production_stages.id |
| item_id | uuid | No | None | FK -> items.id |
| lot_id | uuid | No | None | FK -> lots.id |
| quantity_base | numeric(18,3) | No | None |  |
| source_output_id | uuid | Yes | None | FK -> production_outputs.id |
| stock_line_id | uuid | Yes | None | FK -> stock_document_lines.id |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `quantity_base > 0`

## production_outputs

Group: Manufacturing. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| stage_id | uuid | No | None | FK -> production_stages.id |
| item_id | uuid | No | None | FK -> items.id |
| lot_id | uuid | No | None | FK -> lots.id |
| quantity_base | numeric(18,3) | No | None |  |
| output_kind | varchar(40) | No | None | ; values: FINAL, INTERMEDIATE, BYPRODUCT, RECOVERED |
| stock_line_id | uuid | Yes | None | FK -> stock_document_lines.id |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `quantity_base > 0`

## production_losses

Group: Manufacturing. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| stage_id | uuid | No | None | FK -> production_stages.id |
| quantity_base | numeric(18,3) | No | None |  |
| reason | varchar(600) | No | None |  |
| approved_by | uuid | Yes | None | FK -> app_users.id |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `quantity_base > 0`

## production_costs

Group: Manufacturing. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| order_id | uuid | No | None | FK -> production_orders.id |
| stage_id | uuid | Yes | None | FK -> production_stages.id |
| cost_kind | varchar(40) | No | None | ; values: LABOR, ENERGY, MAINTENANCE, TRANSPORT, OTHER |
| amount | numeric(18,2) | No | None |  |
| expense_document_id | uuid | Yes | None | FK -> documents.id |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `amount >= 0`

## reprocessing_links

Group: Manufacturing. Aggregate allocated sum is checked under a locked source output; no cross-row CHECK fiction.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| source_output_id | uuid | No | None | FK -> production_outputs.id |
| target_order_id | uuid | No | None | FK -> production_orders.id |
| lot_id | uuid | No | None | FK -> lots.id |
| quantity_base | numeric(18,3) | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `source_output_id, target_order_id`

Checks: `quantity_base > 0`

## production_quality_checks

Group: Manufacturing. Disabled until a quality workflow is approved.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| stage_id | uuid | No | None | FK -> production_stages.id |
| results | jsonb | No | None |  |
| enabled | boolean | No | false |  |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `enabled = false`

## agri_seasons

Group: Agriculture. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(50) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| starts_on | date | No | None |  |
| ends_on | date | Yes | None |  |
| status | varchar(40) | No | 'PLANNED' | ; values: PLANNED, ACTIVE, CLOSED |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: `ends_on IS NULL OR ends_on >= starts_on`

## agri_work_units

Group: Agriculture. Work unit/Bayft belongs to season and has many stages; meaning must be approved before business sign-off.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| season_id | uuid | No | None | FK -> agri_seasons.id |
| code | varchar(50) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| area_feddan | numeric(18,3) | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `season_id, code`

Checks: `area_feddan > 0`

## agri_stages

Group: Agriculture. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| work_unit_id | uuid | No | None | FK -> agri_work_units.id |
| name_ar | varchar(160) | No | None |  |
| sequence_no | integer | No | None |  |
| starts_on | date | Yes | None |  |
| ends_on | date | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `work_unit_id, sequence_no`

Checks: `sequence_no > 0`; `ends_on IS NULL OR starts_on IS NULL OR ends_on >= starts_on`

## agri_stage_inputs

Group: Agriculture. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| stage_id | uuid | No | None | FK -> agri_stages.id |
| item_id | uuid | No | None | FK -> items.id |
| stock_document_id | uuid | No | None | FK -> documents.id |
| quantity_base | numeric(18,3) | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `quantity_base > 0`

## equipment

Group: Agriculture. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(50) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| is_active | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## equipment_usage

Group: Agriculture. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| equipment_id | uuid | No | None | FK -> equipment.id |
| agri_stage_id | uuid | No | None | FK -> agri_stages.id |
| usage_date | date | No | None |  |
| hours | numeric(18,3) | No | None |  |
| cost_amount | numeric(18,2) | No | None |  |
| expense_document_id | uuid | Yes | None | FK -> documents.id |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `hours > 0`; `cost_amount >= 0`

## transport_vehicles

Group: Agriculture. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(50) | No | None |  |
| plate_number | varchar(50) | Yes | None |  |
| is_active | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## transport_trips

Group: Agriculture. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| trip_no | varchar(60) | No | None |  |
| vehicle_id | uuid | No | None | FK -> transport_vehicles.id |
| agri_stage_id | uuid | Yes | None | FK -> agri_stages.id |
| source_document_id | uuid | Yes | None | FK -> documents.id |
| trip_date | date | No | None |  |
| load_quantity | numeric(18,3) | Yes | None |  |
| cost_amount | numeric(18,2) | No | None |  |
| expense_document_id | uuid | Yes | None | FK -> documents.id |
| version | bigint | No | 1 |  |

Unique constraints: `trip_no`

Checks: `cost_amount >= 0`; `load_quantity IS NULL OR load_quantity > 0`

## harvest_receipts

Group: Agriculture.  Shared primary key: id is documents.id and is the canonical API document id.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | None | FK -> documents.id |
| agri_stage_id | uuid | No | None | FK -> agri_stages.id |
| item_id | uuid | No | None | FK -> items.id |
| lot_id | uuid | No | None | FK -> lots.id |
| quantity_base | numeric(18,3) | No | None |  |
| warehouse_id | uuid | No | None | FK -> warehouses.id |
| stock_document_id | uuid | Yes | None | FK -> documents.id |

Unique constraints: `stock_document_id`

Checks: `quantity_base > 0`

## agri_expense_links

Group: Agriculture. Reference to one finance expense, never a duplicated cash movement.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| agri_stage_id | uuid | No | None | FK -> agri_stages.id |
| expense_document_id | uuid | No | None | FK -> documents.id |
| version | bigint | No | 1 |  |

Unique constraints: `expense_document_id`

Checks: No additional table CHECK recorded.

## accounts

Group: Finance. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(40) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| parent_id | uuid | Yes | None | FK -> accounts.id |
| account_type | varchar(40) | No | None | ; values: ASSET, LIABILITY, EQUITY, REVENUE, EXPENSE |
| is_postable | boolean | No | false |  |
| is_active | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## cash_bank_accounts

Group: Finance. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| code | varchar(40) | No | None |  |
| name_ar | varchar(160) | No | None |  |
| account_kind | varchar(40) | No | None | ; values: CASH, BANK |
| ledger_account_id | uuid | No | None | FK -> accounts.id |
| is_agricultural | boolean | No | false |  |
| is_active | boolean | No | true |  |
| version | bigint | No | 1 |  |

Unique constraints: `code`

Checks: No additional table CHECK recorded.

## journal_entries

Group: Finance.  Shared primary key: id is documents.id and is the canonical API document id.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | None | FK -> documents.id |
| source_document_id | uuid | Yes | None | FK -> documents.id |
| description | varchar(600) | No | None |  |

Unique constraints: None additional to the primary key.

Checks: No additional table CHECK recorded.

## journal_lines

Group: Finance. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| journal_entry_id | uuid | No | None | FK -> journal_entries.id |
| line_no | integer | No | None |  |
| account_id | uuid | No | None | FK -> accounts.id |
| cost_center_id | uuid | Yes | None | FK -> cost_centers.id |
| debit | numeric(18,2) | No | 0 |  |
| credit | numeric(18,2) | No | 0 |  |
| description | varchar(400) | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `journal_entry_id, line_no`

Checks: `(debit > 0 AND credit=0) OR (credit > 0 AND debit=0)`

## financial_documents

Group: Finance.  Shared primary key: id is documents.id and is the canonical API document id.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | None | FK -> documents.id |
| financial_type | varchar(40) | No | None | ; values: RECEIPT, PAYMENT, TRANSFER |
| cash_bank_account_id | uuid | No | None | FK -> cash_bank_accounts.id |
| counter_account_id | uuid | Yes | None | FK -> cash_bank_accounts.id |
| party_id | uuid | Yes | None | FK -> parties.id |
| amount | numeric(18,2) | No | None |  |
| journal_document_id | uuid | Yes | None | FK -> documents.id |

Unique constraints: None additional to the primary key.

Checks: `amount > 0`; `(financial_type='TRANSFER' AND counter_account_id IS NOT NULL AND counter_account_id<>cash_bank_account_id) OR (financial_type<>'TRANSFER' AND counter_account_id IS NULL)`

## expenses

Group: Finance.  Shared primary key: id is documents.id and is the canonical API document id.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | None | FK -> documents.id |
| cost_center_id | uuid | No | None | FK -> cost_centers.id |
| party_id | uuid | Yes | None | FK -> parties.id |
| amount | numeric(18,2) | No | None |  |
| description | varchar(600) | No | None |  |
| financial_document_id | uuid | Yes | None | FK -> documents.id |
| journal_document_id | uuid | Yes | None | FK -> documents.id |

Unique constraints: None additional to the primary key.

Checks: `amount > 0`

## receivable_payable_items

Group: Finance. Open amount derived from posted settlement allocations, not mutable paid totals.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| party_id | uuid | No | None | FK -> parties.id |
| source_document_id | uuid | No | None | FK -> documents.id |
| item_kind | varchar(40) | No | None | ; values: RECEIVABLE, PAYABLE |
| original_amount | numeric(18,2) | No | None |  |
| due_date | date | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `source_document_id, item_kind`

Checks: `original_amount > 0`

## settlement_allocations

Group: Finance. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| open_item_id | uuid | No | None | FK -> receivable_payable_items.id |
| financial_document_id | uuid | No | None | FK -> documents.id |
| amount | numeric(18,2) | No | None |  |
| reversal_of_id | uuid | Yes | None | FK -> settlement_allocations.id |

Unique constraints: `reversal_of_id`

Checks: `amount > 0`

## cost_allocations

Group: Finance. Disabled valuation/allocation baseline; approval requires a new reviewed migration and contract.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| production_order_id | uuid | No | None | FK -> production_orders.id |
| production_output_id | uuid | No | None | FK -> production_outputs.id |
| amount | numeric(18,2) | No | None |  |
| policy_decision_id | uuid | No | None | FK -> policy_decisions.id |
| enabled | boolean | No | false |  |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `amount >= 0`; `enabled = false`

## purchase_documents

Group: Trading.  Shared primary key: id is documents.id and is the canonical API document id.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | None | FK -> documents.id |
| party_id | uuid | No | None | FK -> parties.id |
| party_snapshot | jsonb | No | None |  |
| warehouse_id | uuid | No | None | FK -> warehouses.id |
| stock_document_id | uuid | Yes | None | FK -> documents.id |
| open_item_id | uuid | Yes | None | FK -> receivable_payable_items.id |

Unique constraints: `stock_document_id`; `open_item_id`

Checks: No additional table CHECK recorded.

## purchase_lines

Group: Trading. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| purchase_document_id | uuid | No | None | FK -> purchase_documents.id |
| line_no | integer | No | None |  |
| item_id | uuid | No | None | FK -> items.id |
| lot_id | uuid | No | None | FK -> lots.id |
| quantity_entered | numeric(18,3) | No | None |  |
| unit_id | uuid | No | None | FK -> units.id |
| conversion_factor | numeric(18,6) | No | None |  |
| unit_price | numeric(18,4) | No | None |  |
| item_name_snapshot | varchar(200) | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `purchase_document_id, line_no`

Checks: `quantity_entered > 0`; `conversion_factor > 0`; `unit_price >= 0`

## sales_documents

Group: Trading.  Shared primary key: id is documents.id and is the canonical API document id.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | None | FK -> documents.id |
| party_id | uuid | No | None | FK -> parties.id |
| party_snapshot | jsonb | No | None |  |
| warehouse_id | uuid | No | None | FK -> warehouses.id |
| stock_document_id | uuid | Yes | None | FK -> documents.id |
| open_item_id | uuid | Yes | None | FK -> receivable_payable_items.id |

Unique constraints: `stock_document_id`; `open_item_id`

Checks: No additional table CHECK recorded.

## sales_lines

Group: Trading. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| sales_document_id | uuid | No | None | FK -> sales_documents.id |
| line_no | integer | No | None |  |
| item_id | uuid | No | None | FK -> items.id |
| lot_id | uuid | No | None | FK -> lots.id |
| quantity_entered | numeric(18,3) | No | None |  |
| unit_id | uuid | No | None | FK -> units.id |
| conversion_factor | numeric(18,6) | No | None |  |
| unit_price | numeric(18,4) | No | None |  |
| item_name_snapshot | varchar(200) | No | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `sales_document_id, line_no`

Checks: `quantity_entered > 0`; `conversion_factor > 0`; `unit_price >= 0`

## notifications

Group: System. SYS-01/SYS-02 proposed only; baseline disabled and endpoints return FEATURE_DISABLED.

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| user_id | uuid | No | None | FK -> app_users.id |
| notification_type | varchar(80) | No | None |  |
| message_ar | varchar(600) | No | None |  |
| source_document_id | uuid | Yes | None | FK -> documents.id |
| created_at | timestamptz | No | now() |  |
| read_at | timestamptz | Yes | None |  |
| enabled | boolean | No | false |  |
| version | bigint | No | 1 |  |

Unique constraints: None additional to the primary key.

Checks: `enabled = false`

## legacy_import_jobs

Group: Operations. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| file_sha256 | varchar(64) | No | None |  |
| mapping_version | varchar(40) | No | None |  |
| state | varchar(40) | No | 'UPLOADED' | ; values: UPLOADED, VALIDATED, REJECTED, IMPORTED |
| requested_by | uuid | No | None | FK -> app_users.id |
| created_at | timestamptz | No | now() |  |
| reconciliation_report_key | varchar(240) | Yes | None |  |
| version | bigint | No | 1 |  |

Unique constraints: `file_sha256, mapping_version`

Checks: No additional table CHECK recorded.

## legacy_import_rows

Group: Operations. 

| Column | PostgreSQL type | Nullable | Default | Reference / permitted values |
| --- | --- | --- | --- | --- |
| id | uuid | No | gen_random_uuid() |  |
| job_id | uuid | No | None | FK -> legacy_import_jobs.id |
| row_number | integer | No | None |  |
| source_data | jsonb | No | None |  |
| validation_errors | jsonb | No | None |  |
| imported_document_id | uuid | Yes | None | FK -> documents.id |
| version | bigint | No | 1 |  |

Unique constraints: `job_id, row_number`

Checks: `row_number > 0`

## Composite relationships

```json
[
  [
    "stock_document_lines",
    [
      "lot_id",
      "item_id"
    ],
    "lots",
    [
      "id",
      "item_id"
    ]
  ],
  [
    "stock_balances",
    [
      "lot_id",
      "item_id"
    ],
    "lots",
    [
      "id",
      "item_id"
    ]
  ],
  [
    "stock_ledger_entries",
    [
      "lot_id",
      "item_id"
    ],
    "lots",
    [
      "id",
      "item_id"
    ]
  ],
  [
    "production_inputs",
    [
      "lot_id",
      "item_id"
    ],
    "lots",
    [
      "id",
      "item_id"
    ]
  ],
  [
    "production_outputs",
    [
      "lot_id",
      "item_id"
    ],
    "lots",
    [
      "id",
      "item_id"
    ]
  ],
  [
    "harvest_receipts",
    [
      "lot_id",
      "item_id"
    ],
    "lots",
    [
      "id",
      "item_id"
    ]
  ],
  [
    "purchase_lines",
    [
      "lot_id",
      "item_id"
    ],
    "lots",
    [
      "id",
      "item_id"
    ]
  ],
  [
    "sales_lines",
    [
      "lot_id",
      "item_id"
    ],
    "lots",
    [
      "id",
      "item_id"
    ]
  ]
]
```

## Typed document kinds

```json
{
  "stock_documents": "STOCK",
  "production_orders": "PRODUCTION",
  "journal_entries": "JOURNAL",
  "financial_documents": "FINANCIAL",
  "expenses": "EXPENSE",
  "purchase_documents": "PURCHASE",
  "sales_documents": "SALE",
  "harvest_receipts": "HARVEST"
}
```
