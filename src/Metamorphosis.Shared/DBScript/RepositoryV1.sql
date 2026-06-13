CREATE TABLE IF NOT EXISTS commits (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER REFERENCES commits(id), branch TEXT NOT NULL DEFAULT 'main', hash TEXT NOT NULL, message TEXT NOT NULL, author TEXT NOT NULL, machine TEXT NOT NULL, timestamp TEXT NOT NULL, timestamp_ticks INTEGER NOT NULL, snapshot_file TEXT NOT NULL, element_count INTEGER, model_path TEXT, revit_version TEXT, document_guid TEXT)
CREATE TABLE IF NOT EXISTS branches (name TEXT PRIMARY KEY, head_id INTEGER REFERENCES commits(id), created_at TEXT NOT NULL)
CREATE TABLE IF NOT EXISTS repo_config (key TEXT PRIMARY KEY, value TEXT)
INSERT OR IGNORE INTO branches (name, head_id, created_at) VALUES ('main', NULL, datetime('now'))
