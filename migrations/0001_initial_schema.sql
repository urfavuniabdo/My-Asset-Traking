-- ============================================================
-- نظام إدارة وتتبع الأصول والدعم الفني
-- Asset Tracking & Ticketing System — Initial Schema (21 tables)
-- ============================================================

-- 1) الشركات
CREATE TABLE IF NOT EXISTS companies (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  name_en TEXT,
  commercial_no TEXT,
  tax_number TEXT,
  address TEXT,
  logo_path TEXT,
  is_active INTEGER NOT NULL DEFAULT 1,
  is_deleted INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 2) الإدارات
CREATE TABLE IF NOT EXISTS departments (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  company_id INTEGER NOT NULL REFERENCES companies(id),
  name TEXT NOT NULL,
  code TEXT,
  manager_user_id INTEGER,
  is_deleted INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 3) المواقع
CREATE TABLE IF NOT EXISTS locations (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  company_id INTEGER NOT NULL REFERENCES companies(id),
  name TEXT NOT NULL,
  type TEXT NOT NULL DEFAULT 'Office', -- Factory|Office|Building|Apartment|Warehouse
  address_details TEXT,
  gps_coordinates TEXT,
  is_deleted INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 4) الموردون
CREATE TABLE IF NOT EXISTS vendors (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  company_id INTEGER REFERENCES companies(id),
  name TEXT NOT NULL,
  contact_person TEXT,
  phone TEXT,
  email TEXT,
  address TEXT,
  is_deleted INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 5) المستخدمون
CREATE TABLE IF NOT EXISTS users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  company_id INTEGER REFERENCES companies(id),
  department_id INTEGER REFERENCES departments(id),
  full_name TEXT NOT NULL,
  email TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL,
  role TEXT NOT NULL DEFAULT 'Employee', -- Admin|CompanyManager|Technician|Employee
  phone_number TEXT,
  job_title TEXT,
  employee_number TEXT,
  is_active INTEGER NOT NULL DEFAULT 1,
  is_deleted INTEGER NOT NULL DEFAULT 0,
  last_login_at TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 6) التصنيفات (شجرية)
CREATE TABLE IF NOT EXISTS categories (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  parent_category_id INTEGER REFERENCES categories(id),
  name TEXT NOT NULL,
  code TEXT,
  default_useful_life_years INTEGER DEFAULT 5,
  default_salvage_rate REAL DEFAULT 0.1,
  is_deleted INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 7) الأصول
CREATE TABLE IF NOT EXISTS assets (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  asset_tag TEXT NOT NULL UNIQUE,           -- AST-2026-00001
  company_id INTEGER NOT NULL REFERENCES companies(id),
  category_id INTEGER REFERENCES categories(id),
  location_id INTEGER REFERENCES locations(id),
  vendor_id INTEGER REFERENCES vendors(id),
  current_custody_user_id INTEGER REFERENCES users(id),
  name TEXT NOT NULL,
  serial_number TEXT,
  barcode TEXT,
  model TEXT,
  brand TEXT,
  status TEXT NOT NULL DEFAULT 'Active',    -- Active|UnderMaintenance|Damaged|Disposed|Lost|InStore
  purchase_cost REAL DEFAULT 0,
  purchase_date TEXT,
  warranty_expiry_date TEXT,
  useful_life_years INTEGER DEFAULT 5,
  salvage_value REAL DEFAULT 0,
  accumulated_depreciation REAL DEFAULT 0,
  book_value REAL DEFAULT 0,
  qr_code_path TEXT,
  notes TEXT,
  is_deleted INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT
);

-- 8) سجل العُهد
CREATE TABLE IF NOT EXISTS custody_logs (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  asset_id INTEGER NOT NULL REFERENCES assets(id),
  previous_user_id INTEGER REFERENCES users(id),
  new_user_id INTEGER REFERENCES users(id),
  action_type TEXT NOT NULL,                -- Assign|Return|Transfer
  acceptance_status TEXT DEFAULT 'Pending', -- Pending|Accepted|Rejected
  accepted_at TEXT,
  transfer_date TEXT,
  reason TEXT,
  rejection_reason TEXT,
  condition_note TEXT,
  assigned_by_user_id INTEGER REFERENCES users(id),
  receipt_doc_path TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 9) سجل المواقع
CREATE TABLE IF NOT EXISTS location_logs (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  asset_id INTEGER NOT NULL REFERENCES assets(id),
  previous_location_id INTEGER REFERENCES locations(id),
  new_location_id INTEGER REFERENCES locations(id),
  transfer_date TEXT,
  reason TEXT,
  moved_by_user_id INTEGER REFERENCES users(id),
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 10) سياسات SLA
CREATE TABLE IF NOT EXISTS sla_policies (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  priority TEXT NOT NULL,          -- Low|Medium|High|Critical
  response_time_hours INTEGER NOT NULL,
  resolution_time_hours INTEGER NOT NULL,
  is_active INTEGER NOT NULL DEFAULT 1
);

-- 11) تذاكر الصيانة
CREATE TABLE IF NOT EXISTS maintenance_tickets (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  ticket_number TEXT NOT NULL UNIQUE,      -- TKT-2026-00001
  asset_id INTEGER NOT NULL REFERENCES assets(id),
  company_id INTEGER NOT NULL REFERENCES companies(id),
  requester_user_id INTEGER REFERENCES users(id),
  assigned_technician_id INTEGER REFERENCES users(id),
  sla_policy_id INTEGER REFERENCES sla_policies(id),
  status TEXT NOT NULL DEFAULT 'Open',     -- Open|Assigned|InProgress|WaitingParts|Resolved|Closed|Cancelled
  priority TEXT NOT NULL DEFAULT 'Medium',
  source TEXT NOT NULL DEFAULT 'Manual',   -- Manual|QRScan|Preventive
  issue_description TEXT,
  resolution_report TEXT,
  labor_cost REAL DEFAULT 0,
  parts_cost REAL DEFAULT 0,
  total_cost REAL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  assigned_at TEXT,
  first_response_at TEXT,
  resolved_at TEXT,
  closed_at TEXT,
  sla_response_due_at TEXT,
  sla_resolution_due_at TEXT,
  sla_breached INTEGER NOT NULL DEFAULT 0
);

-- 12) سجل التذاكر
CREATE TABLE IF NOT EXISTS ticket_logs (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  ticket_id INTEGER NOT NULL REFERENCES maintenance_tickets(id),
  action_user_id INTEGER REFERENCES users(id),
  action_type TEXT NOT NULL,
  old_value TEXT,
  new_value TEXT,
  notes TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 13) تعليقات التذاكر
CREATE TABLE IF NOT EXISTS ticket_comments (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  ticket_id INTEGER NOT NULL REFERENCES maintenance_tickets(id),
  user_id INTEGER REFERENCES users(id),
  comment_text TEXT NOT NULL,
  is_internal INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 14) قطع الغيار
CREATE TABLE IF NOT EXISTS ticket_parts (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  ticket_id INTEGER NOT NULL REFERENCES maintenance_tickets(id),
  part_name TEXT NOT NULL,
  quantity INTEGER NOT NULL DEFAULT 1,
  unit_cost REAL NOT NULL DEFAULT 0,
  total_cost REAL NOT NULL DEFAULT 0,
  supplier_name TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 15) جداول الصيانة الوقائية
CREATE TABLE IF NOT EXISTS maintenance_schedules (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  asset_id INTEGER NOT NULL REFERENCES assets(id),
  title TEXT NOT NULL,
  recurrence_type TEXT NOT NULL,    -- Monthly|Quarterly|SemiAnnual|Annual
  next_due_date TEXT NOT NULL,
  checklist_json TEXT,
  is_active INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 16) قيود الإهلاك
CREATE TABLE IF NOT EXISTS depreciation_entries (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  asset_id INTEGER NOT NULL REFERENCES assets(id),
  period_date TEXT NOT NULL,
  amount REAL NOT NULL,
  book_value_after REAL NOT NULL,
  method TEXT NOT NULL DEFAULT 'StraightLine',
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 17) جلسات الجرد
CREATE TABLE IF NOT EXISTS inventory_audits (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  company_id INTEGER NOT NULL REFERENCES companies(id),
  location_id INTEGER REFERENCES locations(id),
  title TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'Open',  -- Open|InProgress|Completed
  created_by_user_id INTEGER REFERENCES users(id),
  started_at TEXT,
  completed_at TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 18) بنود الجرد
CREATE TABLE IF NOT EXISTS inventory_audit_items (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  inventory_audit_id INTEGER NOT NULL REFERENCES inventory_audits(id),
  asset_id INTEGER NOT NULL REFERENCES assets(id),
  result TEXT NOT NULL DEFAULT 'Expected', -- Expected|Found|Missing|WrongLocation|Damaged
  scanned_by_user_id INTEGER REFERENCES users(id),
  scanned_at TEXT,
  notes TEXT
);

-- 19) المرفقات
CREATE TABLE IF NOT EXISTS attachments (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  entity_type TEXT NOT NULL,
  entity_id INTEGER NOT NULL,
  file_name TEXT NOT NULL,
  file_path TEXT,
  content_type TEXT,
  file_size_bytes INTEGER,
  uploaded_by_user_id INTEGER REFERENCES users(id),
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 20) الإشعارات
CREATE TABLE IF NOT EXISTS notifications (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER NOT NULL REFERENCES users(id),
  title TEXT NOT NULL,
  message TEXT,
  type TEXT NOT NULL DEFAULT 'System',   -- Ticket|Custody|Warranty|SLA|System
  target_url TEXT,
  is_read INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 21) سجل التدقيق
CREATE TABLE IF NOT EXISTS audit_logs (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER REFERENCES users(id),
  entity_name TEXT NOT NULL,
  entity_id TEXT,
  action TEXT NOT NULL,      -- Create|Update|Delete|Login|Export
  changes_json TEXT,
  ip_address TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 22) إعدادات النظام
CREATE TABLE IF NOT EXISTS system_settings (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  setting_key TEXT NOT NULL UNIQUE,
  setting_value TEXT,
  description TEXT
);

-- الجلسات (auth)
CREATE TABLE IF NOT EXISTS sessions (
  token TEXT PRIMARY KEY,
  user_id INTEGER NOT NULL REFERENCES users(id),
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  expires_at TEXT NOT NULL
);

-- ============================================================
-- الفهارس الإلزامية (§6.2)
-- ============================================================
CREATE UNIQUE INDEX IF NOT EXISTS ux_assets_tag ON assets(asset_tag);
CREATE INDEX IF NOT EXISTS ix_assets_company ON assets(company_id, is_deleted);
CREATE INDEX IF NOT EXISTS ix_assets_location ON assets(location_id);
CREATE INDEX IF NOT EXISTS ix_assets_category ON assets(category_id);
CREATE INDEX IF NOT EXISTS ix_assets_custody ON assets(current_custody_user_id);
CREATE INDEX IF NOT EXISTS ix_assets_status ON assets(status);
CREATE INDEX IF NOT EXISTS ix_assets_serial ON assets(serial_number);
CREATE INDEX IF NOT EXISTS ix_tickets_company_status ON maintenance_tickets(company_id, status);
CREATE INDEX IF NOT EXISTS ix_tickets_asset ON maintenance_tickets(asset_id);
CREATE INDEX IF NOT EXISTS ix_tickets_tech ON maintenance_tickets(assigned_technician_id, status);
CREATE INDEX IF NOT EXISTS ix_tickets_requester ON maintenance_tickets(requester_user_id);
CREATE INDEX IF NOT EXISTS ix_tickets_sla ON maintenance_tickets(sla_resolution_due_at, sla_breached);
CREATE INDEX IF NOT EXISTS ix_custody_asset ON custody_logs(asset_id, created_at);
CREATE INDEX IF NOT EXISTS ix_custody_newuser ON custody_logs(new_user_id, acceptance_status);
CREATE INDEX IF NOT EXISTS ix_loclogs_asset ON location_logs(asset_id, created_at);
CREATE INDEX IF NOT EXISTS ix_users_company ON users(company_id, is_deleted);
CREATE INDEX IF NOT EXISTS ix_users_email ON users(email);
CREATE INDEX IF NOT EXISTS ix_notif_user ON notifications(user_id, is_read);
CREATE INDEX IF NOT EXISTS ix_auditlogs_created ON audit_logs(created_at);
CREATE INDEX IF NOT EXISTS ix_sessions_user ON sessions(user_id);
CREATE INDEX IF NOT EXISTS ix_dep_asset ON depreciation_entries(asset_id, period_date);
CREATE INDEX IF NOT EXISTS ix_sched_due ON maintenance_schedules(next_due_date, is_active);
CREATE INDEX IF NOT EXISTS ix_audititems_audit ON inventory_audit_items(inventory_audit_id);
