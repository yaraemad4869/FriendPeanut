-- Friend Peanut design baseline, PostgreSQL 17+. Isolated database only.

BEGIN;

CREATE SCHEMA friend_peanut;

SET search_path TO friend_peanut, public;

CREATE TABLE app_users (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  slot_no smallint NOT NULL,
  username varchar(80) NOT NULL,
  display_name varchar(160) NOT NULL,
  password_hash varchar(512),
  status varchar(40) NOT NULL DEFAULT 'INACTIVE' CHECK (status IN ('INACTIVE', 'ACTIVE', 'LOCKED')),
  must_change_password boolean NOT NULL DEFAULT true,
  remote_access_enabled boolean NOT NULL DEFAULT false,
  failed_login_count integer NOT NULL DEFAULT 0,
  security_stamp uuid NOT NULL DEFAULT gen_random_uuid(),
  version bigint NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (slot_no),
  UNIQUE (username),
  CHECK (slot_no BETWEEN 1 AND 15),
  CHECK (failed_login_count >= 0),
  CHECK (status <> 'ACTIVE' OR password_hash IS NOT NULL)
);

COMMENT ON TABLE app_users IS '15 preallocated slots; enabling remote access is separate from roles.';

CREATE TABLE roles (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(60) NOT NULL,
  name_ar varchar(160) NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE permissions (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(100) NOT NULL,
  description_ar varchar(240) NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE user_roles (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  role_id uuid NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (user_id, role_id)
);

CREATE TABLE role_permissions (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  role_id uuid NOT NULL,
  permission_id uuid NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (role_id, permission_id)
);

CREATE TABLE user_scopes (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  scope_kind varchar(40) NOT NULL CHECK (scope_kind IN ('WAREHOUSE', 'FACTORY', 'DEPARTMENT')),
  warehouse_id uuid,
  factory_id uuid,
  department_code varchar(60),
  version bigint NOT NULL DEFAULT 1,
  CHECK ((scope_kind='WAREHOUSE' AND warehouse_id IS NOT NULL AND factory_id IS NULL AND department_code IS NULL) OR (scope_kind='FACTORY' AND factory_id IS NOT NULL AND warehouse_id IS NULL AND department_code IS NULL) OR (scope_kind='DEPARTMENT' AND department_code IS NOT NULL AND warehouse_id IS NULL AND factory_id IS NULL))
);

COMMENT ON TABLE user_scopes IS 'No NULL-means-all interpretation. Global scope is an explicit permission policy.';

CREATE TABLE registered_devices (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  external_device_key varchar(200) NOT NULL,
  display_name varchar(160) NOT NULL,
  status varchar(40) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'APPROVED', 'REVOKED')),
  approved_by uuid,
  approved_at timestamptz,
  revoked_at timestamptz,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (external_device_key)
);

CREATE TABLE user_devices (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  device_id uuid NOT NULL,
  remote_allowed boolean NOT NULL DEFAULT false,
  created_at timestamptz NOT NULL DEFAULT now(),
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (user_id, device_id)
);

CREATE TABLE auth_sessions (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  token_hash varchar(128) NOT NULL,
  device_id uuid,
  access_channel varchar(40) NOT NULL CHECK (access_channel IN ('LAN', 'VPN')),
  created_at timestamptz NOT NULL DEFAULT now(),
  expires_at timestamptz NOT NULL,
  revoked_at timestamptz,
  mfa_verified_at timestamptz,
  security_stamp uuid NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (token_hash),
  CHECK (expires_at > created_at)
);

COMMENT ON TABLE auth_sessions IS 'Cookie contains an opaque token; only hash stored. Device identity comes from a trusted gateway.';

CREATE TABLE auth_challenges (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  challenge_hash varchar(128) NOT NULL,
  next_step varchar(40) NOT NULL CHECK (next_step IN ('CHANGE_PASSWORD', 'MFA_SETUP', 'MFA_VERIFY')),
  expires_at timestamptz NOT NULL,
  consumed_at timestamptz,
  attempts integer NOT NULL DEFAULT 0,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (challenge_hash),
  CHECK (attempts >= 0)
);

CREATE TABLE mfa_credentials (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  secret_ciphertext varchar(1024) NOT NULL,
  encryption_key_id varchar(100) NOT NULL,
  is_confirmed boolean NOT NULL DEFAULT false,
  last_accepted_time_step bigint,
  confirmed_at timestamptz,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (user_id)
);

COMMENT ON TABLE mfa_credentials IS 'Encrypted TOTP secret, never in logs, exports or user DTOs.';

CREATE TABLE mfa_recovery_codes (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  code_hash varchar(128) NOT NULL,
  used_at timestamptz,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code_hash)
);

CREATE TABLE login_attempts (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid,
  occurred_at timestamptz NOT NULL DEFAULT now(),
  succeeded boolean NOT NULL DEFAULT false,
  failure_code varchar(80),
  source_ip inet,
  correlation_id varchar(80) NOT NULL,
  version bigint NOT NULL DEFAULT 1
);

COMMENT ON TABLE login_attempts IS 'Restricted security metadata with approved retention; no password, OTP or token.';

CREATE TABLE factories (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(20) NOT NULL,
  name_ar varchar(160) NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code),
  CHECK (code IN ('F1_SORTING','F2_PEELING'))
);

CREATE TABLE sites (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(30) NOT NULL,
  name_ar varchar(160) NOT NULL,
  is_active boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE warehouses (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(30) NOT NULL,
  name_ar varchar(160) NOT NULL,
  site_id uuid,
  factory_id uuid,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code),
  CHECK (code IN ('WH_AGRI','WH_SORTED','WH_PEELED','WH_PACKAGING'))
);

COMMENT ON TABLE warehouses IS 'Exactly four seeded warehouses, no create/delete API.';

CREATE TABLE item_categories (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(40) NOT NULL,
  name_ar varchar(160) NOT NULL,
  parent_id uuid,
  is_active boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE units (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(20) NOT NULL,
  name_ar varchar(80) NOT NULL,
  dimension varchar(40) NOT NULL CHECK (dimension IN ('MASS', 'COUNT', 'VOLUME', 'LENGTH')),
  is_active boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE items (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(50) NOT NULL,
  name_ar varchar(200) NOT NULL,
  normalized_name varchar(200) NOT NULL,
  category_id uuid NOT NULL,
  base_unit_id uuid,
  unit_status varchar(40) NOT NULL DEFAULT 'PENDING' CHECK (unit_status IN ('PENDING', 'APPROVED')),
  lot_tracking_enabled boolean NOT NULL DEFAULT true,
  can_purchase boolean NOT NULL DEFAULT false,
  can_sell boolean NOT NULL DEFAULT false,
  can_manufacture boolean NOT NULL DEFAULT false,
  is_active boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code),
  CHECK (unit_status <> 'APPROVED' OR base_unit_id IS NOT NULL)
);

COMMENT ON TABLE items IS 'Mass items use KG base. Non-mass units remain blocked until approved; no fabricated bag weight.';

CREATE TABLE item_aliases (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  item_id uuid NOT NULL,
  alias_ar varchar(200) NOT NULL,
  normalized_alias varchar(200) NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (normalized_alias)
);

CREATE TABLE unit_conversions (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  item_id uuid NOT NULL,
  from_unit_id uuid NOT NULL,
  to_unit_id uuid NOT NULL,
  factor numeric(18,6) NOT NULL,
  valid_from date NOT NULL,
  valid_to date,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (item_id, from_unit_id, to_unit_id, valid_from),
  CHECK (factor > 0),
  CHECK (from_unit_id <> to_unit_id),
  CHECK (valid_to IS NULL OR valid_to >= valid_from)
);

CREATE TABLE lots (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  item_id uuid NOT NULL,
  code varchar(60) NOT NULL,
  is_system_untracked boolean NOT NULL DEFAULT false,
  received_date date,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (item_id, code),
  UNIQUE (id, item_id)
);

COMMENT ON TABLE lots IS 'Untracked items resolve to one system lot. Ledger lot_id is never NULL.';

CREATE TABLE item_warehouse_rules (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  item_id uuid NOT NULL,
  warehouse_id uuid NOT NULL,
  is_allowed boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (item_id, warehouse_id)
);

CREATE TABLE parties (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(50) NOT NULL,
  name_ar varchar(200) NOT NULL,
  phone varchar(40),
  address varchar(400),
  tax_number varchar(60),
  is_active boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE party_roles (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  party_id uuid NOT NULL,
  role_code varchar(40) NOT NULL CHECK (role_code IN ('CUSTOMER', 'SUPPLIER', 'CONTRACTOR')),
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (party_id, role_code)
);

CREATE TABLE party_contacts (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  party_id uuid NOT NULL,
  display_name varchar(160) NOT NULL,
  phone varchar(40),
  email varchar(200),
  version bigint NOT NULL DEFAULT 1
);

CREATE TABLE cost_centers (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(40) NOT NULL,
  name_ar varchar(160) NOT NULL,
  parent_id uuid,
  is_active boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE documents (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  document_no varchar(60) NOT NULL,
  document_type varchar(40) NOT NULL CHECK (document_type IN ('STOCK', 'PRODUCTION', 'JOURNAL', 'FINANCIAL', 'PURCHASE', 'SALE', 'EXPENSE', 'HARVEST')),
  document_date date NOT NULL,
  status varchar(40) NOT NULL DEFAULT 'DRAFT' CHECK (status IN ('DRAFT', 'SUBMITTED', 'APPROVED', 'POSTED', 'REJECTED', 'CANCELLED')),
  created_by uuid NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  approved_by uuid,
  approved_at timestamptz,
  posted_by uuid,
  posted_at timestamptz,
  reason varchar(600),
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (document_no),
  CHECK (status <> 'POSTED' OR (posted_at IS NOT NULL AND posted_by IS NOT NULL))
);

COMMENT ON TABLE documents IS 'Posted rows immutable. REVERSED is derived via document_reversals, not a header update.';

CREATE TABLE document_sequences (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  sequence_key varchar(40) NOT NULL,
  year smallint NOT NULL,
  last_number bigint NOT NULL DEFAULT 0,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (sequence_key, year),
  CHECK (last_number >= 0)
);

CREATE TABLE document_reversals (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  original_document_id uuid NOT NULL,
  reversal_document_id uuid NOT NULL,
  reason varchar(600) NOT NULL,
  created_by uuid NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (original_document_id),
  UNIQUE (reversal_document_id),
  CHECK (original_document_id <> reversal_document_id)
);

CREATE TABLE document_attachments (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  document_id uuid NOT NULL,
  storage_key varchar(240) NOT NULL,
  original_name varchar(200) NOT NULL,
  content_type varchar(100) NOT NULL,
  byte_count bigint NOT NULL,
  sha256 varchar(64) NOT NULL,
  uploaded_by uuid NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (storage_key),
  CHECK (byte_count > 0)
);

CREATE TABLE idempotency_records (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  route_key varchar(200) NOT NULL,
  idempotency_key varchar(100) NOT NULL,
  request_hash varchar(64) NOT NULL,
  state varchar(40) NOT NULL DEFAULT 'IN_PROGRESS' CHECK (state IN ('IN_PROGRESS', 'COMPLETED')),
  response_status integer,
  response_body jsonb,
  expires_at timestamptz NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (user_id, route_key, idempotency_key)
);

CREATE TABLE audit_events (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  actor_id uuid,
  action varchar(100) NOT NULL,
  entity_type varchar(100) NOT NULL,
  entity_id uuid,
  before_redacted jsonb,
  after_redacted jsonb,
  reason varchar(600),
  correlation_id varchar(80) NOT NULL,
  occurred_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE policy_decisions (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  policy_key varchar(100) NOT NULL,
  status varchar(40) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'APPROVED', 'RETIRED')),
  version bigint NOT NULL DEFAULT 1,
  approved_configuration jsonb,
  approved_by uuid,
  approved_at timestamptz,
  UNIQUE (policy_key),
  CHECK (status <> 'APPROVED' OR (approved_by IS NOT NULL AND approved_at IS NOT NULL))
);

CREATE TABLE settings (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  key varchar(100) NOT NULL,
  value jsonb NOT NULL,
  policy_decision_id uuid,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (key)
);

CREATE TABLE stock_documents (
  id uuid PRIMARY KEY NOT NULL,
  movement_type varchar(40) NOT NULL CHECK (movement_type IN ('OPENING', 'PURCHASE_RECEIPT', 'HARVEST_RECEIPT', 'PRODUCTION_ISSUE', 'PRODUCTION_RECEIPT', 'AGRI_ISSUE', 'SALES_ISSUE', 'TRANSFER', 'PURCHASE_RETURN', 'SALES_RETURN', 'ADJUSTMENT', 'INTERMEDIATE_RECEIPT', 'INTERMEDIATE_ISSUE', 'REVERSAL')),
  warehouse_id uuid NOT NULL,
  counter_warehouse_id uuid,
  party_id uuid,
  party_snapshot jsonb,
  source_document_id uuid,
  production_stage_id uuid,
  CHECK ((movement_type='TRANSFER' AND counter_warehouse_id IS NOT NULL AND warehouse_id<>counter_warehouse_id) OR (movement_type<>'TRANSFER' AND counter_warehouse_id IS NULL))
);

COMMENT ON TABLE stock_documents IS ' Shared primary key: id is documents.id and is the canonical API document id.';

CREATE TABLE stock_document_lines (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  stock_document_id uuid NOT NULL,
  line_no integer NOT NULL,
  item_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  quantity_entered numeric(18,3) NOT NULL,
  unit_id uuid NOT NULL,
  conversion_factor numeric(18,6) NOT NULL,
  quantity_base numeric(18,3) NOT NULL,
  base_unit_id uuid NOT NULL,
  direction smallint NOT NULL,
  unit_price numeric(18,4),
  item_name_snapshot varchar(200) NOT NULL,
  unit_code_snapshot varchar(20) NOT NULL,
  description varchar(400),
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (stock_document_id, line_no),
  CHECK (quantity_entered > 0),
  CHECK (conversion_factor > 0),
  CHECK (quantity_base > 0),
  CHECK (direction IN (-1,1)),
  CHECK (unit_price IS NULL OR unit_price >= 0)
);

CREATE TABLE posting_batches (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  document_id uuid NOT NULL,
  posted_by uuid NOT NULL,
  posted_at timestamptz NOT NULL DEFAULT now(),
  engine_version varchar(40) NOT NULL,
  UNIQUE (document_id)
);

CREATE TABLE stock_ledger_entries (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  posting_batch_id uuid NOT NULL,
  stock_line_id uuid NOT NULL,
  item_id uuid NOT NULL,
  warehouse_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  base_unit_id uuid NOT NULL,
  quantity_delta numeric(18,3) NOT NULL,
  unit_cost numeric(18,4),
  posted_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (posting_batch_id, stock_line_id, warehouse_id),
  CHECK (quantity_delta <> 0)
);

CREATE TABLE stock_balances (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  item_id uuid NOT NULL,
  warehouse_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  base_unit_id uuid NOT NULL,
  quantity_base numeric(18,3) NOT NULL DEFAULT 0,
  negative_override_authorized boolean NOT NULL DEFAULT false,
  negative_override_reason varchar(600),
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (item_id, warehouse_id, lot_id),
  CHECK (quantity_base >= 0 OR (negative_override_authorized AND length(trim(coalesce(negative_override_reason,'')))>0))
);

COMMENT ON TABLE stock_balances IS 'Derived cache. Application cannot use the flag without stock.allow_negative and an audited per-posting reason.';

CREATE TABLE stock_counts (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  count_no varchar(60) NOT NULL,
  warehouse_id uuid NOT NULL,
  count_date date NOT NULL,
  status varchar(40) NOT NULL DEFAULT 'DRAFT' CHECK (status IN ('DRAFT', 'FINALIZED', 'CANCELLED')),
  snapshot_at timestamptz NOT NULL,
  adjustment_document_id uuid,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (count_no),
  UNIQUE (adjustment_document_id)
);

CREATE TABLE stock_count_lines (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  stock_count_id uuid NOT NULL,
  item_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  snapshot_quantity numeric(18,3) NOT NULL,
  counted_quantity numeric(18,3) NOT NULL,
  reason varchar(600),
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (stock_count_id, item_id, lot_id),
  CHECK (counted_quantity >= 0)
);

CREATE TABLE stock_reservations (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  item_id uuid NOT NULL,
  warehouse_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  quantity_base numeric(18,3) NOT NULL,
  enabled boolean NOT NULL DEFAULT false,
  version bigint NOT NULL DEFAULT 1,
  CHECK (quantity_base > 0),
  CHECK (enabled = false)
);

COMMENT ON TABLE stock_reservations IS 'Disabled future extension; no API/UI in the baseline.';

CREATE TABLE production_orders (
  id uuid PRIMARY KEY NOT NULL,
  factory_id uuid NOT NULL,
  responsible_user_id uuid NOT NULL,
  parent_order_id uuid
);

COMMENT ON TABLE production_orders IS ' Shared primary key: id is documents.id and is the canonical API document id.';

CREATE TABLE production_stages (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  order_id uuid NOT NULL,
  sequence_no integer NOT NULL,
  stage_type varchar(40) NOT NULL CHECK (stage_type IN ('SORTING', 'OVEN', 'PEELING', 'SECOND_SORT', 'REPROCESSING')),
  nesba_path varchar(40) CHECK (nesba_path IN ('DIRECT', 'STORED')),
  status varchar(40) NOT NULL DEFAULT 'DRAFT' CHECK (status IN ('DRAFT', 'READY', 'POSTED')),
  input_quantity numeric(18,3) NOT NULL DEFAULT 0,
  output_quantity numeric(18,3) NOT NULL DEFAULT 0,
  loss_quantity numeric(18,3) NOT NULL DEFAULT 0,
  stock_document_id uuid,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (order_id, sequence_no),
  UNIQUE (stock_document_id),
  CHECK (sequence_no > 0),
  CHECK (input_quantity >= 0),
  CHECK (output_quantity >= 0),
  CHECK (loss_quantity >= 0)
);

CREATE TABLE production_inputs (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  stage_id uuid NOT NULL,
  item_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  quantity_base numeric(18,3) NOT NULL,
  source_output_id uuid,
  stock_line_id uuid,
  version bigint NOT NULL DEFAULT 1,
  CHECK (quantity_base > 0)
);

CREATE TABLE production_outputs (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  stage_id uuid NOT NULL,
  item_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  quantity_base numeric(18,3) NOT NULL,
  output_kind varchar(40) NOT NULL CHECK (output_kind IN ('FINAL', 'INTERMEDIATE', 'BYPRODUCT', 'RECOVERED')),
  stock_line_id uuid,
  version bigint NOT NULL DEFAULT 1,
  CHECK (quantity_base > 0)
);

CREATE TABLE production_losses (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  stage_id uuid NOT NULL,
  quantity_base numeric(18,3) NOT NULL,
  reason varchar(600) NOT NULL,
  approved_by uuid,
  version bigint NOT NULL DEFAULT 1,
  CHECK (quantity_base > 0)
);

CREATE TABLE production_costs (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  order_id uuid NOT NULL,
  stage_id uuid,
  cost_kind varchar(40) NOT NULL CHECK (cost_kind IN ('LABOR', 'ENERGY', 'MAINTENANCE', 'TRANSPORT', 'OTHER')),
  amount numeric(18,2) NOT NULL,
  expense_document_id uuid,
  version bigint NOT NULL DEFAULT 1,
  CHECK (amount >= 0)
);

CREATE TABLE reprocessing_links (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  source_output_id uuid NOT NULL,
  target_order_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  quantity_base numeric(18,3) NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (source_output_id, target_order_id),
  CHECK (quantity_base > 0)
);

COMMENT ON TABLE reprocessing_links IS 'Aggregate allocated sum is checked under a locked source output; no cross-row CHECK fiction.';

CREATE TABLE production_quality_checks (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  stage_id uuid NOT NULL,
  results jsonb NOT NULL,
  enabled boolean NOT NULL DEFAULT false,
  version bigint NOT NULL DEFAULT 1,
  CHECK (enabled = false)
);

COMMENT ON TABLE production_quality_checks IS 'Disabled until a quality workflow is approved.';

CREATE TABLE agri_seasons (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(50) NOT NULL,
  name_ar varchar(160) NOT NULL,
  starts_on date NOT NULL,
  ends_on date,
  status varchar(40) NOT NULL DEFAULT 'PLANNED' CHECK (status IN ('PLANNED', 'ACTIVE', 'CLOSED')),
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code),
  CHECK (ends_on IS NULL OR ends_on >= starts_on)
);

CREATE TABLE agri_work_units (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  season_id uuid NOT NULL,
  code varchar(50) NOT NULL,
  name_ar varchar(160) NOT NULL,
  area_feddan numeric(18,3) NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (season_id, code),
  CHECK (area_feddan > 0)
);

COMMENT ON TABLE agri_work_units IS 'Work unit/Ø¨ÙŠÙØª belongs to season and has many stages; meaning must be approved before business sign-off.';

CREATE TABLE agri_stages (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  work_unit_id uuid NOT NULL,
  name_ar varchar(160) NOT NULL,
  sequence_no integer NOT NULL,
  starts_on date,
  ends_on date,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (work_unit_id, sequence_no),
  CHECK (sequence_no > 0),
  CHECK (ends_on IS NULL OR starts_on IS NULL OR ends_on >= starts_on)
);

CREATE TABLE agri_stage_inputs (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  stage_id uuid NOT NULL,
  item_id uuid NOT NULL,
  stock_document_id uuid NOT NULL,
  quantity_base numeric(18,3) NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  CHECK (quantity_base > 0)
);

CREATE TABLE equipment (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(50) NOT NULL,
  name_ar varchar(160) NOT NULL,
  is_active boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE equipment_usage (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  equipment_id uuid NOT NULL,
  agri_stage_id uuid NOT NULL,
  usage_date date NOT NULL,
  hours numeric(18,3) NOT NULL,
  cost_amount numeric(18,2) NOT NULL,
  expense_document_id uuid,
  version bigint NOT NULL DEFAULT 1,
  CHECK (hours > 0),
  CHECK (cost_amount >= 0)
);

CREATE TABLE transport_vehicles (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(50) NOT NULL,
  plate_number varchar(50),
  is_active boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE transport_trips (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  trip_no varchar(60) NOT NULL,
  vehicle_id uuid NOT NULL,
  agri_stage_id uuid,
  source_document_id uuid,
  trip_date date NOT NULL,
  load_quantity numeric(18,3),
  cost_amount numeric(18,2) NOT NULL,
  expense_document_id uuid,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (trip_no),
  CHECK (cost_amount >= 0),
  CHECK (load_quantity IS NULL OR load_quantity > 0)
);

CREATE TABLE harvest_receipts (
  id uuid PRIMARY KEY NOT NULL,
  agri_stage_id uuid NOT NULL,
  item_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  quantity_base numeric(18,3) NOT NULL,
  warehouse_id uuid NOT NULL,
  stock_document_id uuid,
  UNIQUE (stock_document_id),
  CHECK (quantity_base > 0)
);

COMMENT ON TABLE harvest_receipts IS ' Shared primary key: id is documents.id and is the canonical API document id.';

CREATE TABLE agri_expense_links (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  agri_stage_id uuid NOT NULL,
  expense_document_id uuid NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (expense_document_id)
);

COMMENT ON TABLE agri_expense_links IS 'Reference to one finance expense, never a duplicated cash movement.';

CREATE TABLE accounts (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(40) NOT NULL,
  name_ar varchar(160) NOT NULL,
  parent_id uuid,
  account_type varchar(40) NOT NULL CHECK (account_type IN ('ASSET', 'LIABILITY', 'EQUITY', 'REVENUE', 'EXPENSE')),
  is_postable boolean NOT NULL DEFAULT false,
  is_active boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE cash_bank_accounts (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  code varchar(40) NOT NULL,
  name_ar varchar(160) NOT NULL,
  account_kind varchar(40) NOT NULL CHECK (account_kind IN ('CASH', 'BANK')),
  ledger_account_id uuid NOT NULL,
  is_agricultural boolean NOT NULL DEFAULT false,
  is_active boolean NOT NULL DEFAULT true,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (code)
);

CREATE TABLE journal_entries (
  id uuid PRIMARY KEY NOT NULL,
  source_document_id uuid,
  description varchar(600) NOT NULL
);

COMMENT ON TABLE journal_entries IS ' Shared primary key: id is documents.id and is the canonical API document id.';

CREATE TABLE journal_lines (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  journal_entry_id uuid NOT NULL,
  line_no integer NOT NULL,
  account_id uuid NOT NULL,
  cost_center_id uuid,
  debit numeric(18,2) NOT NULL DEFAULT 0,
  credit numeric(18,2) NOT NULL DEFAULT 0,
  description varchar(400),
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (journal_entry_id, line_no),
  CHECK ((debit > 0 AND credit=0) OR (credit > 0 AND debit=0))
);

CREATE TABLE financial_documents (
  id uuid PRIMARY KEY NOT NULL,
  financial_type varchar(40) NOT NULL CHECK (financial_type IN ('RECEIPT', 'PAYMENT', 'TRANSFER')),
  cash_bank_account_id uuid NOT NULL,
  counter_account_id uuid,
  party_id uuid,
  amount numeric(18,2) NOT NULL,
  journal_document_id uuid,
  CHECK (amount > 0),
  CHECK ((financial_type='TRANSFER' AND counter_account_id IS NOT NULL AND counter_account_id<>cash_bank_account_id) OR (financial_type<>'TRANSFER' AND counter_account_id IS NULL))
);

COMMENT ON TABLE financial_documents IS ' Shared primary key: id is documents.id and is the canonical API document id.';

CREATE TABLE expenses (
  id uuid PRIMARY KEY NOT NULL,
  cost_center_id uuid NOT NULL,
  party_id uuid,
  amount numeric(18,2) NOT NULL,
  description varchar(600) NOT NULL,
  financial_document_id uuid,
  journal_document_id uuid,
  CHECK (amount > 0)
);

COMMENT ON TABLE expenses IS ' Shared primary key: id is documents.id and is the canonical API document id.';

CREATE TABLE receivable_payable_items (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  party_id uuid NOT NULL,
  source_document_id uuid NOT NULL,
  item_kind varchar(40) NOT NULL CHECK (item_kind IN ('RECEIVABLE', 'PAYABLE')),
  original_amount numeric(18,2) NOT NULL,
  due_date date,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (source_document_id, item_kind),
  CHECK (original_amount > 0)
);

COMMENT ON TABLE receivable_payable_items IS 'Open amount derived from posted settlement allocations, not mutable paid totals.';

CREATE TABLE settlement_allocations (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  open_item_id uuid NOT NULL,
  financial_document_id uuid NOT NULL,
  amount numeric(18,2) NOT NULL,
  reversal_of_id uuid,
  UNIQUE (reversal_of_id),
  CHECK (amount > 0)
);

CREATE TABLE cost_allocations (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  production_order_id uuid NOT NULL,
  production_output_id uuid NOT NULL,
  amount numeric(18,2) NOT NULL,
  policy_decision_id uuid NOT NULL,
  enabled boolean NOT NULL DEFAULT false,
  version bigint NOT NULL DEFAULT 1,
  CHECK (amount >= 0),
  CHECK (enabled = false)
);

COMMENT ON TABLE cost_allocations IS 'Disabled valuation/allocation baseline; approval requires a new reviewed migration and contract.';

CREATE TABLE purchase_documents (
  id uuid PRIMARY KEY NOT NULL,
  party_id uuid NOT NULL,
  party_snapshot jsonb NOT NULL,
  warehouse_id uuid NOT NULL,
  stock_document_id uuid,
  open_item_id uuid,
  UNIQUE (stock_document_id),
  UNIQUE (open_item_id)
);

COMMENT ON TABLE purchase_documents IS ' Shared primary key: id is documents.id and is the canonical API document id.';

CREATE TABLE purchase_lines (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  purchase_document_id uuid NOT NULL,
  line_no integer NOT NULL,
  item_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  quantity_entered numeric(18,3) NOT NULL,
  unit_id uuid NOT NULL,
  conversion_factor numeric(18,6) NOT NULL,
  unit_price numeric(18,4) NOT NULL,
  item_name_snapshot varchar(200) NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (purchase_document_id, line_no),
  CHECK (quantity_entered > 0),
  CHECK (conversion_factor > 0),
  CHECK (unit_price >= 0)
);

CREATE TABLE sales_documents (
  id uuid PRIMARY KEY NOT NULL,
  party_id uuid NOT NULL,
  party_snapshot jsonb NOT NULL,
  warehouse_id uuid NOT NULL,
  stock_document_id uuid,
  open_item_id uuid,
  UNIQUE (stock_document_id),
  UNIQUE (open_item_id)
);

COMMENT ON TABLE sales_documents IS ' Shared primary key: id is documents.id and is the canonical API document id.';

CREATE TABLE sales_lines (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  sales_document_id uuid NOT NULL,
  line_no integer NOT NULL,
  item_id uuid NOT NULL,
  lot_id uuid NOT NULL,
  quantity_entered numeric(18,3) NOT NULL,
  unit_id uuid NOT NULL,
  conversion_factor numeric(18,6) NOT NULL,
  unit_price numeric(18,4) NOT NULL,
  item_name_snapshot varchar(200) NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (sales_document_id, line_no),
  CHECK (quantity_entered > 0),
  CHECK (conversion_factor > 0),
  CHECK (unit_price >= 0)
);

CREATE TABLE notifications (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL,
  notification_type varchar(80) NOT NULL,
  message_ar varchar(600) NOT NULL,
  source_document_id uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  read_at timestamptz,
  enabled boolean NOT NULL DEFAULT false,
  version bigint NOT NULL DEFAULT 1,
  CHECK (enabled = false)
);

COMMENT ON TABLE notifications IS 'SYS-01/SYS-02 proposed only; baseline disabled and endpoints return FEATURE_DISABLED.';

CREATE TABLE legacy_import_jobs (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  file_sha256 varchar(64) NOT NULL,
  mapping_version varchar(40) NOT NULL,
  state varchar(40) NOT NULL DEFAULT 'UPLOADED' CHECK (state IN ('UPLOADED', 'VALIDATED', 'REJECTED', 'IMPORTED')),
  requested_by uuid NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  reconciliation_report_key varchar(240),
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (file_sha256, mapping_version)
);

CREATE TABLE legacy_import_rows (
  id uuid PRIMARY KEY NOT NULL DEFAULT gen_random_uuid(),
  job_id uuid NOT NULL,
  row_number integer NOT NULL,
  source_data jsonb NOT NULL,
  validation_errors jsonb NOT NULL,
  imported_document_id uuid,
  version bigint NOT NULL DEFAULT 1,
  UNIQUE (job_id, row_number),
  CHECK (row_number > 0)
);

ALTER TABLE user_roles ADD CONSTRAINT fk_user_roles_user_id FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_user_roles_user_id ON user_roles (user_id);

ALTER TABLE user_roles ADD CONSTRAINT fk_user_roles_role_id FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE RESTRICT;

CREATE INDEX ix_user_roles_role_id ON user_roles (role_id);

ALTER TABLE role_permissions ADD CONSTRAINT fk_role_permissions_role_id FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE RESTRICT;

CREATE INDEX ix_role_permissions_role_id ON role_permissions (role_id);

ALTER TABLE role_permissions ADD CONSTRAINT fk_role_permissions_permission_id FOREIGN KEY (permission_id) REFERENCES permissions(id) ON DELETE RESTRICT;

CREATE INDEX ix_role_permissions_permission_id ON role_permissions (permission_id);

ALTER TABLE user_scopes ADD CONSTRAINT fk_user_scopes_user_id FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_user_scopes_user_id ON user_scopes (user_id);

ALTER TABLE user_scopes ADD CONSTRAINT fk_user_scopes_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_user_scopes_warehouse_id ON user_scopes (warehouse_id);

ALTER TABLE user_scopes ADD CONSTRAINT fk_user_scopes_factory_id FOREIGN KEY (factory_id) REFERENCES factories(id) ON DELETE RESTRICT;

CREATE INDEX ix_user_scopes_factory_id ON user_scopes (factory_id);

ALTER TABLE registered_devices ADD CONSTRAINT fk_registered_devices_approved_by FOREIGN KEY (approved_by) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_registered_devices_approved_by ON registered_devices (approved_by);

ALTER TABLE user_devices ADD CONSTRAINT fk_user_devices_user_id FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_user_devices_user_id ON user_devices (user_id);

ALTER TABLE user_devices ADD CONSTRAINT fk_user_devices_device_id FOREIGN KEY (device_id) REFERENCES registered_devices(id) ON DELETE RESTRICT;

CREATE INDEX ix_user_devices_device_id ON user_devices (device_id);

ALTER TABLE auth_sessions ADD CONSTRAINT fk_auth_sessions_user_id FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_auth_sessions_user_id ON auth_sessions (user_id);

ALTER TABLE auth_sessions ADD CONSTRAINT fk_auth_sessions_device_id FOREIGN KEY (device_id) REFERENCES registered_devices(id) ON DELETE RESTRICT;

CREATE INDEX ix_auth_sessions_device_id ON auth_sessions (device_id);

ALTER TABLE auth_challenges ADD CONSTRAINT fk_auth_challenges_user_id FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_auth_challenges_user_id ON auth_challenges (user_id);

ALTER TABLE mfa_credentials ADD CONSTRAINT fk_mfa_credentials_user_id FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_mfa_credentials_user_id ON mfa_credentials (user_id);

ALTER TABLE mfa_recovery_codes ADD CONSTRAINT fk_mfa_recovery_codes_user_id FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_mfa_recovery_codes_user_id ON mfa_recovery_codes (user_id);

ALTER TABLE login_attempts ADD CONSTRAINT fk_login_attempts_user_id FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_login_attempts_user_id ON login_attempts (user_id);

ALTER TABLE warehouses ADD CONSTRAINT fk_warehouses_site_id FOREIGN KEY (site_id) REFERENCES sites(id) ON DELETE RESTRICT;

CREATE INDEX ix_warehouses_site_id ON warehouses (site_id);

ALTER TABLE warehouses ADD CONSTRAINT fk_warehouses_factory_id FOREIGN KEY (factory_id) REFERENCES factories(id) ON DELETE RESTRICT;

CREATE INDEX ix_warehouses_factory_id ON warehouses (factory_id);

ALTER TABLE item_categories ADD CONSTRAINT fk_item_categories_parent_id FOREIGN KEY (parent_id) REFERENCES item_categories(id) ON DELETE RESTRICT;

CREATE INDEX ix_item_categories_parent_id ON item_categories (parent_id);

ALTER TABLE items ADD CONSTRAINT fk_items_category_id FOREIGN KEY (category_id) REFERENCES item_categories(id) ON DELETE RESTRICT;

CREATE INDEX ix_items_category_id ON items (category_id);

ALTER TABLE items ADD CONSTRAINT fk_items_base_unit_id FOREIGN KEY (base_unit_id) REFERENCES units(id) ON DELETE RESTRICT;

CREATE INDEX ix_items_base_unit_id ON items (base_unit_id);

ALTER TABLE item_aliases ADD CONSTRAINT fk_item_aliases_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_item_aliases_item_id ON item_aliases (item_id);

ALTER TABLE unit_conversions ADD CONSTRAINT fk_unit_conversions_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_unit_conversions_item_id ON unit_conversions (item_id);

ALTER TABLE unit_conversions ADD CONSTRAINT fk_unit_conversions_from_unit_id FOREIGN KEY (from_unit_id) REFERENCES units(id) ON DELETE RESTRICT;

CREATE INDEX ix_unit_conversions_from_unit_id ON unit_conversions (from_unit_id);

ALTER TABLE unit_conversions ADD CONSTRAINT fk_unit_conversions_to_unit_id FOREIGN KEY (to_unit_id) REFERENCES units(id) ON DELETE RESTRICT;

CREATE INDEX ix_unit_conversions_to_unit_id ON unit_conversions (to_unit_id);

ALTER TABLE lots ADD CONSTRAINT fk_lots_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_lots_item_id ON lots (item_id);

ALTER TABLE item_warehouse_rules ADD CONSTRAINT fk_item_warehouse_rules_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_item_warehouse_rules_item_id ON item_warehouse_rules (item_id);

ALTER TABLE item_warehouse_rules ADD CONSTRAINT fk_item_warehouse_rules_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_item_warehouse_rules_warehouse_id ON item_warehouse_rules (warehouse_id);

ALTER TABLE party_roles ADD CONSTRAINT fk_party_roles_party_id FOREIGN KEY (party_id) REFERENCES parties(id) ON DELETE RESTRICT;

CREATE INDEX ix_party_roles_party_id ON party_roles (party_id);

ALTER TABLE party_contacts ADD CONSTRAINT fk_party_contacts_party_id FOREIGN KEY (party_id) REFERENCES parties(id) ON DELETE RESTRICT;

CREATE INDEX ix_party_contacts_party_id ON party_contacts (party_id);

ALTER TABLE cost_centers ADD CONSTRAINT fk_cost_centers_parent_id FOREIGN KEY (parent_id) REFERENCES cost_centers(id) ON DELETE RESTRICT;

CREATE INDEX ix_cost_centers_parent_id ON cost_centers (parent_id);

ALTER TABLE documents ADD CONSTRAINT fk_documents_created_by FOREIGN KEY (created_by) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_documents_created_by ON documents (created_by);

ALTER TABLE documents ADD CONSTRAINT fk_documents_approved_by FOREIGN KEY (approved_by) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_documents_approved_by ON documents (approved_by);

ALTER TABLE documents ADD CONSTRAINT fk_documents_posted_by FOREIGN KEY (posted_by) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_documents_posted_by ON documents (posted_by);

ALTER TABLE document_reversals ADD CONSTRAINT fk_document_reversals_original_document_id FOREIGN KEY (original_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_document_reversals_original_document_id ON document_reversals (original_document_id);

ALTER TABLE document_reversals ADD CONSTRAINT fk_document_reversals_reversal_document_id FOREIGN KEY (reversal_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_document_reversals_reversal_document_id ON document_reversals (reversal_document_id);

ALTER TABLE document_reversals ADD CONSTRAINT fk_document_reversals_created_by FOREIGN KEY (created_by) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_document_reversals_created_by ON document_reversals (created_by);

ALTER TABLE document_attachments ADD CONSTRAINT fk_document_attachments_document_id FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_document_attachments_document_id ON document_attachments (document_id);

ALTER TABLE document_attachments ADD CONSTRAINT fk_document_attachments_uploaded_by FOREIGN KEY (uploaded_by) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_document_attachments_uploaded_by ON document_attachments (uploaded_by);

ALTER TABLE idempotency_records ADD CONSTRAINT fk_idempotency_records_user_id FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_idempotency_records_user_id ON idempotency_records (user_id);

ALTER TABLE audit_events ADD CONSTRAINT fk_audit_events_actor_id FOREIGN KEY (actor_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_audit_events_actor_id ON audit_events (actor_id);

ALTER TABLE policy_decisions ADD CONSTRAINT fk_policy_decisions_approved_by FOREIGN KEY (approved_by) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_policy_decisions_approved_by ON policy_decisions (approved_by);

ALTER TABLE settings ADD CONSTRAINT fk_settings_policy_decision_id FOREIGN KEY (policy_decision_id) REFERENCES policy_decisions(id) ON DELETE RESTRICT;

CREATE INDEX ix_settings_policy_decision_id ON settings (policy_decision_id);

ALTER TABLE stock_documents ADD CONSTRAINT fk_stock_documents_id FOREIGN KEY (id) REFERENCES documents(id) ON DELETE RESTRICT;

ALTER TABLE stock_documents ADD CONSTRAINT fk_stock_documents_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_documents_warehouse_id ON stock_documents (warehouse_id);

ALTER TABLE stock_documents ADD CONSTRAINT fk_stock_documents_counter_warehouse_id FOREIGN KEY (counter_warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_documents_counter_warehouse_id ON stock_documents (counter_warehouse_id);

ALTER TABLE stock_documents ADD CONSTRAINT fk_stock_documents_party_id FOREIGN KEY (party_id) REFERENCES parties(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_documents_party_id ON stock_documents (party_id);

ALTER TABLE stock_documents ADD CONSTRAINT fk_stock_documents_source_document_id FOREIGN KEY (source_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_documents_source_document_id ON stock_documents (source_document_id);

ALTER TABLE stock_documents ADD CONSTRAINT fk_stock_documents_production_stage_id FOREIGN KEY (production_stage_id) REFERENCES production_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_documents_production_stage_id ON stock_documents (production_stage_id);

ALTER TABLE stock_document_lines ADD CONSTRAINT fk_stock_document_lines_stock_document_id FOREIGN KEY (stock_document_id) REFERENCES stock_documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_document_lines_stock_document_id ON stock_document_lines (stock_document_id);

ALTER TABLE stock_document_lines ADD CONSTRAINT fk_stock_document_lines_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_document_lines_item_id ON stock_document_lines (item_id);

ALTER TABLE stock_document_lines ADD CONSTRAINT fk_stock_document_lines_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_document_lines_lot_id ON stock_document_lines (lot_id);

ALTER TABLE stock_document_lines ADD CONSTRAINT fk_stock_document_lines_unit_id FOREIGN KEY (unit_id) REFERENCES units(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_document_lines_unit_id ON stock_document_lines (unit_id);

ALTER TABLE stock_document_lines ADD CONSTRAINT fk_stock_document_lines_base_unit_id FOREIGN KEY (base_unit_id) REFERENCES units(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_document_lines_base_unit_id ON stock_document_lines (base_unit_id);

ALTER TABLE posting_batches ADD CONSTRAINT fk_posting_batches_document_id FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_posting_batches_document_id ON posting_batches (document_id);

ALTER TABLE posting_batches ADD CONSTRAINT fk_posting_batches_posted_by FOREIGN KEY (posted_by) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_posting_batches_posted_by ON posting_batches (posted_by);

ALTER TABLE stock_ledger_entries ADD CONSTRAINT fk_stock_ledger_entries_posting_batch_id FOREIGN KEY (posting_batch_id) REFERENCES posting_batches(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_ledger_entries_posting_batch_id ON stock_ledger_entries (posting_batch_id);

ALTER TABLE stock_ledger_entries ADD CONSTRAINT fk_stock_ledger_entries_stock_line_id FOREIGN KEY (stock_line_id) REFERENCES stock_document_lines(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_ledger_entries_stock_line_id ON stock_ledger_entries (stock_line_id);

ALTER TABLE stock_ledger_entries ADD CONSTRAINT fk_stock_ledger_entries_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_ledger_entries_item_id ON stock_ledger_entries (item_id);

ALTER TABLE stock_ledger_entries ADD CONSTRAINT fk_stock_ledger_entries_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_ledger_entries_warehouse_id ON stock_ledger_entries (warehouse_id);

ALTER TABLE stock_ledger_entries ADD CONSTRAINT fk_stock_ledger_entries_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_ledger_entries_lot_id ON stock_ledger_entries (lot_id);

ALTER TABLE stock_ledger_entries ADD CONSTRAINT fk_stock_ledger_entries_base_unit_id FOREIGN KEY (base_unit_id) REFERENCES units(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_ledger_entries_base_unit_id ON stock_ledger_entries (base_unit_id);

ALTER TABLE stock_balances ADD CONSTRAINT fk_stock_balances_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_balances_item_id ON stock_balances (item_id);

ALTER TABLE stock_balances ADD CONSTRAINT fk_stock_balances_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_balances_warehouse_id ON stock_balances (warehouse_id);

ALTER TABLE stock_balances ADD CONSTRAINT fk_stock_balances_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_balances_lot_id ON stock_balances (lot_id);

ALTER TABLE stock_balances ADD CONSTRAINT fk_stock_balances_base_unit_id FOREIGN KEY (base_unit_id) REFERENCES units(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_balances_base_unit_id ON stock_balances (base_unit_id);

ALTER TABLE stock_counts ADD CONSTRAINT fk_stock_counts_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_counts_warehouse_id ON stock_counts (warehouse_id);

ALTER TABLE stock_counts ADD CONSTRAINT fk_stock_counts_adjustment_document_id FOREIGN KEY (adjustment_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_counts_adjustment_document_id ON stock_counts (adjustment_document_id);

ALTER TABLE stock_count_lines ADD CONSTRAINT fk_stock_count_lines_stock_count_id FOREIGN KEY (stock_count_id) REFERENCES stock_counts(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_count_lines_stock_count_id ON stock_count_lines (stock_count_id);

ALTER TABLE stock_count_lines ADD CONSTRAINT fk_stock_count_lines_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_count_lines_item_id ON stock_count_lines (item_id);

ALTER TABLE stock_count_lines ADD CONSTRAINT fk_stock_count_lines_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_count_lines_lot_id ON stock_count_lines (lot_id);

ALTER TABLE stock_reservations ADD CONSTRAINT fk_stock_reservations_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_reservations_item_id ON stock_reservations (item_id);

ALTER TABLE stock_reservations ADD CONSTRAINT fk_stock_reservations_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_reservations_warehouse_id ON stock_reservations (warehouse_id);

ALTER TABLE stock_reservations ADD CONSTRAINT fk_stock_reservations_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_stock_reservations_lot_id ON stock_reservations (lot_id);

ALTER TABLE production_orders ADD CONSTRAINT fk_production_orders_id FOREIGN KEY (id) REFERENCES documents(id) ON DELETE RESTRICT;

ALTER TABLE production_orders ADD CONSTRAINT fk_production_orders_factory_id FOREIGN KEY (factory_id) REFERENCES factories(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_orders_factory_id ON production_orders (factory_id);

ALTER TABLE production_orders ADD CONSTRAINT fk_production_orders_responsible_user_id FOREIGN KEY (responsible_user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_orders_responsible_user_id ON production_orders (responsible_user_id);

ALTER TABLE production_orders ADD CONSTRAINT fk_production_orders_parent_order_id FOREIGN KEY (parent_order_id) REFERENCES production_orders(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_orders_parent_order_id ON production_orders (parent_order_id);

ALTER TABLE production_stages ADD CONSTRAINT fk_production_stages_order_id FOREIGN KEY (order_id) REFERENCES production_orders(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_stages_order_id ON production_stages (order_id);

ALTER TABLE production_stages ADD CONSTRAINT fk_production_stages_stock_document_id FOREIGN KEY (stock_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_stages_stock_document_id ON production_stages (stock_document_id);

ALTER TABLE production_inputs ADD CONSTRAINT fk_production_inputs_stage_id FOREIGN KEY (stage_id) REFERENCES production_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_inputs_stage_id ON production_inputs (stage_id);

ALTER TABLE production_inputs ADD CONSTRAINT fk_production_inputs_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_inputs_item_id ON production_inputs (item_id);

ALTER TABLE production_inputs ADD CONSTRAINT fk_production_inputs_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_inputs_lot_id ON production_inputs (lot_id);

ALTER TABLE production_inputs ADD CONSTRAINT fk_production_inputs_source_output_id FOREIGN KEY (source_output_id) REFERENCES production_outputs(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_inputs_source_output_id ON production_inputs (source_output_id);

ALTER TABLE production_inputs ADD CONSTRAINT fk_production_inputs_stock_line_id FOREIGN KEY (stock_line_id) REFERENCES stock_document_lines(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_inputs_stock_line_id ON production_inputs (stock_line_id);

ALTER TABLE production_outputs ADD CONSTRAINT fk_production_outputs_stage_id FOREIGN KEY (stage_id) REFERENCES production_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_outputs_stage_id ON production_outputs (stage_id);

ALTER TABLE production_outputs ADD CONSTRAINT fk_production_outputs_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_outputs_item_id ON production_outputs (item_id);

ALTER TABLE production_outputs ADD CONSTRAINT fk_production_outputs_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_outputs_lot_id ON production_outputs (lot_id);

ALTER TABLE production_outputs ADD CONSTRAINT fk_production_outputs_stock_line_id FOREIGN KEY (stock_line_id) REFERENCES stock_document_lines(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_outputs_stock_line_id ON production_outputs (stock_line_id);

ALTER TABLE production_losses ADD CONSTRAINT fk_production_losses_stage_id FOREIGN KEY (stage_id) REFERENCES production_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_losses_stage_id ON production_losses (stage_id);

ALTER TABLE production_losses ADD CONSTRAINT fk_production_losses_approved_by FOREIGN KEY (approved_by) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_losses_approved_by ON production_losses (approved_by);

ALTER TABLE production_costs ADD CONSTRAINT fk_production_costs_order_id FOREIGN KEY (order_id) REFERENCES production_orders(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_costs_order_id ON production_costs (order_id);

ALTER TABLE production_costs ADD CONSTRAINT fk_production_costs_stage_id FOREIGN KEY (stage_id) REFERENCES production_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_costs_stage_id ON production_costs (stage_id);

ALTER TABLE production_costs ADD CONSTRAINT fk_production_costs_expense_document_id FOREIGN KEY (expense_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_costs_expense_document_id ON production_costs (expense_document_id);

ALTER TABLE reprocessing_links ADD CONSTRAINT fk_reprocessing_links_source_output_id FOREIGN KEY (source_output_id) REFERENCES production_outputs(id) ON DELETE RESTRICT;

CREATE INDEX ix_reprocessing_links_source_output_id ON reprocessing_links (source_output_id);

ALTER TABLE reprocessing_links ADD CONSTRAINT fk_reprocessing_links_target_order_id FOREIGN KEY (target_order_id) REFERENCES production_orders(id) ON DELETE RESTRICT;

CREATE INDEX ix_reprocessing_links_target_order_id ON reprocessing_links (target_order_id);

ALTER TABLE reprocessing_links ADD CONSTRAINT fk_reprocessing_links_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_reprocessing_links_lot_id ON reprocessing_links (lot_id);

ALTER TABLE production_quality_checks ADD CONSTRAINT fk_production_quality_checks_stage_id FOREIGN KEY (stage_id) REFERENCES production_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_production_quality_checks_stage_id ON production_quality_checks (stage_id);

ALTER TABLE agri_work_units ADD CONSTRAINT fk_agri_work_units_season_id FOREIGN KEY (season_id) REFERENCES agri_seasons(id) ON DELETE RESTRICT;

CREATE INDEX ix_agri_work_units_season_id ON agri_work_units (season_id);

ALTER TABLE agri_stages ADD CONSTRAINT fk_agri_stages_work_unit_id FOREIGN KEY (work_unit_id) REFERENCES agri_work_units(id) ON DELETE RESTRICT;

CREATE INDEX ix_agri_stages_work_unit_id ON agri_stages (work_unit_id);

ALTER TABLE agri_stage_inputs ADD CONSTRAINT fk_agri_stage_inputs_stage_id FOREIGN KEY (stage_id) REFERENCES agri_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_agri_stage_inputs_stage_id ON agri_stage_inputs (stage_id);

ALTER TABLE agri_stage_inputs ADD CONSTRAINT fk_agri_stage_inputs_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_agri_stage_inputs_item_id ON agri_stage_inputs (item_id);

ALTER TABLE agri_stage_inputs ADD CONSTRAINT fk_agri_stage_inputs_stock_document_id FOREIGN KEY (stock_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_agri_stage_inputs_stock_document_id ON agri_stage_inputs (stock_document_id);

ALTER TABLE equipment_usage ADD CONSTRAINT fk_equipment_usage_equipment_id FOREIGN KEY (equipment_id) REFERENCES equipment(id) ON DELETE RESTRICT;

CREATE INDEX ix_equipment_usage_equipment_id ON equipment_usage (equipment_id);

ALTER TABLE equipment_usage ADD CONSTRAINT fk_equipment_usage_agri_stage_id FOREIGN KEY (agri_stage_id) REFERENCES agri_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_equipment_usage_agri_stage_id ON equipment_usage (agri_stage_id);

ALTER TABLE equipment_usage ADD CONSTRAINT fk_equipment_usage_expense_document_id FOREIGN KEY (expense_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_equipment_usage_expense_document_id ON equipment_usage (expense_document_id);

ALTER TABLE transport_trips ADD CONSTRAINT fk_transport_trips_vehicle_id FOREIGN KEY (vehicle_id) REFERENCES transport_vehicles(id) ON DELETE RESTRICT;

CREATE INDEX ix_transport_trips_vehicle_id ON transport_trips (vehicle_id);

ALTER TABLE transport_trips ADD CONSTRAINT fk_transport_trips_agri_stage_id FOREIGN KEY (agri_stage_id) REFERENCES agri_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_transport_trips_agri_stage_id ON transport_trips (agri_stage_id);

ALTER TABLE transport_trips ADD CONSTRAINT fk_transport_trips_source_document_id FOREIGN KEY (source_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_transport_trips_source_document_id ON transport_trips (source_document_id);

ALTER TABLE transport_trips ADD CONSTRAINT fk_transport_trips_expense_document_id FOREIGN KEY (expense_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_transport_trips_expense_document_id ON transport_trips (expense_document_id);

ALTER TABLE harvest_receipts ADD CONSTRAINT fk_harvest_receipts_id FOREIGN KEY (id) REFERENCES documents(id) ON DELETE RESTRICT;

ALTER TABLE harvest_receipts ADD CONSTRAINT fk_harvest_receipts_agri_stage_id FOREIGN KEY (agri_stage_id) REFERENCES agri_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_harvest_receipts_agri_stage_id ON harvest_receipts (agri_stage_id);

ALTER TABLE harvest_receipts ADD CONSTRAINT fk_harvest_receipts_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_harvest_receipts_item_id ON harvest_receipts (item_id);

ALTER TABLE harvest_receipts ADD CONSTRAINT fk_harvest_receipts_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_harvest_receipts_lot_id ON harvest_receipts (lot_id);

ALTER TABLE harvest_receipts ADD CONSTRAINT fk_harvest_receipts_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_harvest_receipts_warehouse_id ON harvest_receipts (warehouse_id);

ALTER TABLE harvest_receipts ADD CONSTRAINT fk_harvest_receipts_stock_document_id FOREIGN KEY (stock_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_harvest_receipts_stock_document_id ON harvest_receipts (stock_document_id);

ALTER TABLE agri_expense_links ADD CONSTRAINT fk_agri_expense_links_agri_stage_id FOREIGN KEY (agri_stage_id) REFERENCES agri_stages(id) ON DELETE RESTRICT;

CREATE INDEX ix_agri_expense_links_agri_stage_id ON agri_expense_links (agri_stage_id);

ALTER TABLE agri_expense_links ADD CONSTRAINT fk_agri_expense_links_expense_document_id FOREIGN KEY (expense_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_agri_expense_links_expense_document_id ON agri_expense_links (expense_document_id);

ALTER TABLE accounts ADD CONSTRAINT fk_accounts_parent_id FOREIGN KEY (parent_id) REFERENCES accounts(id) ON DELETE RESTRICT;

CREATE INDEX ix_accounts_parent_id ON accounts (parent_id);

ALTER TABLE cash_bank_accounts ADD CONSTRAINT fk_cash_bank_accounts_ledger_account_id FOREIGN KEY (ledger_account_id) REFERENCES accounts(id) ON DELETE RESTRICT;

CREATE INDEX ix_cash_bank_accounts_ledger_account_id ON cash_bank_accounts (ledger_account_id);

ALTER TABLE journal_entries ADD CONSTRAINT fk_journal_entries_id FOREIGN KEY (id) REFERENCES documents(id) ON DELETE RESTRICT;

ALTER TABLE journal_entries ADD CONSTRAINT fk_journal_entries_source_document_id FOREIGN KEY (source_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_journal_entries_source_document_id ON journal_entries (source_document_id);

ALTER TABLE journal_lines ADD CONSTRAINT fk_journal_lines_journal_entry_id FOREIGN KEY (journal_entry_id) REFERENCES journal_entries(id) ON DELETE RESTRICT;

CREATE INDEX ix_journal_lines_journal_entry_id ON journal_lines (journal_entry_id);

ALTER TABLE journal_lines ADD CONSTRAINT fk_journal_lines_account_id FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE RESTRICT;

CREATE INDEX ix_journal_lines_account_id ON journal_lines (account_id);

ALTER TABLE journal_lines ADD CONSTRAINT fk_journal_lines_cost_center_id FOREIGN KEY (cost_center_id) REFERENCES cost_centers(id) ON DELETE RESTRICT;

CREATE INDEX ix_journal_lines_cost_center_id ON journal_lines (cost_center_id);

ALTER TABLE financial_documents ADD CONSTRAINT fk_financial_documents_id FOREIGN KEY (id) REFERENCES documents(id) ON DELETE RESTRICT;

ALTER TABLE financial_documents ADD CONSTRAINT fk_financial_documents_cash_bank_account_id FOREIGN KEY (cash_bank_account_id) REFERENCES cash_bank_accounts(id) ON DELETE RESTRICT;

CREATE INDEX ix_financial_documents_cash_bank_account_id ON financial_documents (cash_bank_account_id);

ALTER TABLE financial_documents ADD CONSTRAINT fk_financial_documents_counter_account_id FOREIGN KEY (counter_account_id) REFERENCES cash_bank_accounts(id) ON DELETE RESTRICT;

CREATE INDEX ix_financial_documents_counter_account_id ON financial_documents (counter_account_id);

ALTER TABLE financial_documents ADD CONSTRAINT fk_financial_documents_party_id FOREIGN KEY (party_id) REFERENCES parties(id) ON DELETE RESTRICT;

CREATE INDEX ix_financial_documents_party_id ON financial_documents (party_id);

ALTER TABLE financial_documents ADD CONSTRAINT fk_financial_documents_journal_document_id FOREIGN KEY (journal_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_financial_documents_journal_document_id ON financial_documents (journal_document_id);

ALTER TABLE expenses ADD CONSTRAINT fk_expenses_id FOREIGN KEY (id) REFERENCES documents(id) ON DELETE RESTRICT;

ALTER TABLE expenses ADD CONSTRAINT fk_expenses_cost_center_id FOREIGN KEY (cost_center_id) REFERENCES cost_centers(id) ON DELETE RESTRICT;

CREATE INDEX ix_expenses_cost_center_id ON expenses (cost_center_id);

ALTER TABLE expenses ADD CONSTRAINT fk_expenses_party_id FOREIGN KEY (party_id) REFERENCES parties(id) ON DELETE RESTRICT;

CREATE INDEX ix_expenses_party_id ON expenses (party_id);

ALTER TABLE expenses ADD CONSTRAINT fk_expenses_financial_document_id FOREIGN KEY (financial_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_expenses_financial_document_id ON expenses (financial_document_id);

ALTER TABLE expenses ADD CONSTRAINT fk_expenses_journal_document_id FOREIGN KEY (journal_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_expenses_journal_document_id ON expenses (journal_document_id);

ALTER TABLE receivable_payable_items ADD CONSTRAINT fk_receivable_payable_items_party_id FOREIGN KEY (party_id) REFERENCES parties(id) ON DELETE RESTRICT;

CREATE INDEX ix_receivable_payable_items_party_id ON receivable_payable_items (party_id);

ALTER TABLE receivable_payable_items ADD CONSTRAINT fk_receivable_payable_items_source_document_id FOREIGN KEY (source_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_receivable_payable_items_source_document_id ON receivable_payable_items (source_document_id);

ALTER TABLE settlement_allocations ADD CONSTRAINT fk_settlement_allocations_open_item_id FOREIGN KEY (open_item_id) REFERENCES receivable_payable_items(id) ON DELETE RESTRICT;

CREATE INDEX ix_settlement_allocations_open_item_id ON settlement_allocations (open_item_id);

ALTER TABLE settlement_allocations ADD CONSTRAINT fk_settlement_allocations_financial_document_id FOREIGN KEY (financial_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_settlement_allocations_financial_document_id ON settlement_allocations (financial_document_id);

ALTER TABLE settlement_allocations ADD CONSTRAINT fk_settlement_allocations_reversal_of_id FOREIGN KEY (reversal_of_id) REFERENCES settlement_allocations(id) ON DELETE RESTRICT;

CREATE INDEX ix_settlement_allocations_reversal_of_id ON settlement_allocations (reversal_of_id);

ALTER TABLE cost_allocations ADD CONSTRAINT fk_cost_allocations_production_order_id FOREIGN KEY (production_order_id) REFERENCES production_orders(id) ON DELETE RESTRICT;

CREATE INDEX ix_cost_allocations_production_order_id ON cost_allocations (production_order_id);

ALTER TABLE cost_allocations ADD CONSTRAINT fk_cost_allocations_production_output_id FOREIGN KEY (production_output_id) REFERENCES production_outputs(id) ON DELETE RESTRICT;

CREATE INDEX ix_cost_allocations_production_output_id ON cost_allocations (production_output_id);

ALTER TABLE cost_allocations ADD CONSTRAINT fk_cost_allocations_policy_decision_id FOREIGN KEY (policy_decision_id) REFERENCES policy_decisions(id) ON DELETE RESTRICT;

CREATE INDEX ix_cost_allocations_policy_decision_id ON cost_allocations (policy_decision_id);

ALTER TABLE purchase_documents ADD CONSTRAINT fk_purchase_documents_id FOREIGN KEY (id) REFERENCES documents(id) ON DELETE RESTRICT;

ALTER TABLE purchase_documents ADD CONSTRAINT fk_purchase_documents_party_id FOREIGN KEY (party_id) REFERENCES parties(id) ON DELETE RESTRICT;

CREATE INDEX ix_purchase_documents_party_id ON purchase_documents (party_id);

ALTER TABLE purchase_documents ADD CONSTRAINT fk_purchase_documents_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_purchase_documents_warehouse_id ON purchase_documents (warehouse_id);

ALTER TABLE purchase_documents ADD CONSTRAINT fk_purchase_documents_stock_document_id FOREIGN KEY (stock_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_purchase_documents_stock_document_id ON purchase_documents (stock_document_id);

ALTER TABLE purchase_documents ADD CONSTRAINT fk_purchase_documents_open_item_id FOREIGN KEY (open_item_id) REFERENCES receivable_payable_items(id) ON DELETE RESTRICT;

CREATE INDEX ix_purchase_documents_open_item_id ON purchase_documents (open_item_id);

ALTER TABLE purchase_lines ADD CONSTRAINT fk_purchase_lines_purchase_document_id FOREIGN KEY (purchase_document_id) REFERENCES purchase_documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_purchase_lines_purchase_document_id ON purchase_lines (purchase_document_id);

ALTER TABLE purchase_lines ADD CONSTRAINT fk_purchase_lines_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_purchase_lines_item_id ON purchase_lines (item_id);

ALTER TABLE purchase_lines ADD CONSTRAINT fk_purchase_lines_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_purchase_lines_lot_id ON purchase_lines (lot_id);

ALTER TABLE purchase_lines ADD CONSTRAINT fk_purchase_lines_unit_id FOREIGN KEY (unit_id) REFERENCES units(id) ON DELETE RESTRICT;

CREATE INDEX ix_purchase_lines_unit_id ON purchase_lines (unit_id);

ALTER TABLE sales_documents ADD CONSTRAINT fk_sales_documents_id FOREIGN KEY (id) REFERENCES documents(id) ON DELETE RESTRICT;

ALTER TABLE sales_documents ADD CONSTRAINT fk_sales_documents_party_id FOREIGN KEY (party_id) REFERENCES parties(id) ON DELETE RESTRICT;

CREATE INDEX ix_sales_documents_party_id ON sales_documents (party_id);

ALTER TABLE sales_documents ADD CONSTRAINT fk_sales_documents_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses(id) ON DELETE RESTRICT;

CREATE INDEX ix_sales_documents_warehouse_id ON sales_documents (warehouse_id);

ALTER TABLE sales_documents ADD CONSTRAINT fk_sales_documents_stock_document_id FOREIGN KEY (stock_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_sales_documents_stock_document_id ON sales_documents (stock_document_id);

ALTER TABLE sales_documents ADD CONSTRAINT fk_sales_documents_open_item_id FOREIGN KEY (open_item_id) REFERENCES receivable_payable_items(id) ON DELETE RESTRICT;

CREATE INDEX ix_sales_documents_open_item_id ON sales_documents (open_item_id);

ALTER TABLE sales_lines ADD CONSTRAINT fk_sales_lines_sales_document_id FOREIGN KEY (sales_document_id) REFERENCES sales_documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_sales_lines_sales_document_id ON sales_lines (sales_document_id);

ALTER TABLE sales_lines ADD CONSTRAINT fk_sales_lines_item_id FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE RESTRICT;

CREATE INDEX ix_sales_lines_item_id ON sales_lines (item_id);

ALTER TABLE sales_lines ADD CONSTRAINT fk_sales_lines_lot_id FOREIGN KEY (lot_id) REFERENCES lots(id) ON DELETE RESTRICT;

CREATE INDEX ix_sales_lines_lot_id ON sales_lines (lot_id);

ALTER TABLE sales_lines ADD CONSTRAINT fk_sales_lines_unit_id FOREIGN KEY (unit_id) REFERENCES units(id) ON DELETE RESTRICT;

CREATE INDEX ix_sales_lines_unit_id ON sales_lines (unit_id);

ALTER TABLE notifications ADD CONSTRAINT fk_notifications_user_id FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_notifications_user_id ON notifications (user_id);

ALTER TABLE notifications ADD CONSTRAINT fk_notifications_source_document_id FOREIGN KEY (source_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_notifications_source_document_id ON notifications (source_document_id);

ALTER TABLE legacy_import_jobs ADD CONSTRAINT fk_legacy_import_jobs_requested_by FOREIGN KEY (requested_by) REFERENCES app_users(id) ON DELETE RESTRICT;

CREATE INDEX ix_legacy_import_jobs_requested_by ON legacy_import_jobs (requested_by);

ALTER TABLE legacy_import_rows ADD CONSTRAINT fk_legacy_import_rows_job_id FOREIGN KEY (job_id) REFERENCES legacy_import_jobs(id) ON DELETE RESTRICT;

CREATE INDEX ix_legacy_import_rows_job_id ON legacy_import_rows (job_id);

ALTER TABLE legacy_import_rows ADD CONSTRAINT fk_legacy_import_rows_imported_document_id FOREIGN KEY (imported_document_id) REFERENCES documents(id) ON DELETE RESTRICT;

CREATE INDEX ix_legacy_import_rows_imported_document_id ON legacy_import_rows (imported_document_id);

ALTER TABLE stock_document_lines ADD CONSTRAINT fk_stock_document_lines_lot_item_match FOREIGN KEY (lot_id, item_id) REFERENCES lots (id, item_id) ON DELETE RESTRICT;

ALTER TABLE stock_balances ADD CONSTRAINT fk_stock_balances_lot_item_match FOREIGN KEY (lot_id, item_id) REFERENCES lots (id, item_id) ON DELETE RESTRICT;

ALTER TABLE stock_ledger_entries ADD CONSTRAINT fk_stock_ledger_entries_lot_item_match FOREIGN KEY (lot_id, item_id) REFERENCES lots (id, item_id) ON DELETE RESTRICT;

ALTER TABLE production_inputs ADD CONSTRAINT fk_production_inputs_lot_item_match FOREIGN KEY (lot_id, item_id) REFERENCES lots (id, item_id) ON DELETE RESTRICT;

ALTER TABLE production_outputs ADD CONSTRAINT fk_production_outputs_lot_item_match FOREIGN KEY (lot_id, item_id) REFERENCES lots (id, item_id) ON DELETE RESTRICT;

ALTER TABLE harvest_receipts ADD CONSTRAINT fk_harvest_receipts_lot_item_match FOREIGN KEY (lot_id, item_id) REFERENCES lots (id, item_id) ON DELETE RESTRICT;

ALTER TABLE purchase_lines ADD CONSTRAINT fk_purchase_lines_lot_item_match FOREIGN KEY (lot_id, item_id) REFERENCES lots (id, item_id) ON DELETE RESTRICT;

ALTER TABLE sales_lines ADD CONSTRAINT fk_sales_lines_lot_item_match FOREIGN KEY (lot_id, item_id) REFERENCES lots (id, item_id) ON DELETE RESTRICT;

ALTER TABLE stock_document_lines ADD CONSTRAINT ck_stock_line_conversion_exact CHECK (quantity_base = quantity_entered * conversion_factor);

CREATE INDEX ix_ledger_item_warehouse_time ON stock_ledger_entries(item_id, warehouse_id, posted_at, id);

CREATE INDEX ix_documents_status_date ON documents(status, document_date, id);

CREATE INDEX ix_documents_type_date ON documents(document_type, document_date, id);

CREATE INDEX ix_item_normalized_name ON items(normalized_name, id);

CREATE INDEX ix_sessions_expiry ON auth_sessions(expires_at) WHERE revoked_at IS NULL;

CREATE INDEX ix_audit_time ON audit_events(occurred_at, id);

CREATE INDEX ix_audit_entity ON audit_events(entity_type, entity_id, occurred_at);

CREATE INDEX ix_idempotency_expiry ON idempotency_records(expires_at);

CREATE VIEW document_effective_status AS
 SELECT d.*, CASE WHEN r.id IS NOT NULL THEN 'REVERSED' ELSE d.status END AS effective_status
 FROM documents d LEFT JOIN document_reversals r ON r.original_document_id=d.id;

CREATE VIEW stock_balances_from_ledger AS
 SELECT item_id,warehouse_id,lot_id,base_unit_id,SUM(quantity_delta)::numeric(18,3) AS quantity_base
 FROM stock_ledger_entries GROUP BY item_id,warehouse_id,lot_id,base_unit_id;

COMMIT;
