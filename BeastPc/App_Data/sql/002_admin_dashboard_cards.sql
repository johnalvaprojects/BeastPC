-- BeastPC: configurable admin dashboard KPI cards
-- Run once in HeidiSQL (or it is auto-created on first dashboard load).

CREATE TABLE IF NOT EXISTS admin_dashboard_cards (
  id INT NOT NULL AUTO_INCREMENT,
  sort_order INT NOT NULL DEFAULT 0,
  title VARCHAR(160) NOT NULL,
  subtitle VARCHAR(220) NULL,
  accent VARCHAR(120) NULL,
  metric_key VARCHAR(40) NOT NULL,
  literal_value VARCHAR(220) NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  KEY ix_sort (sort_order, id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO admin_dashboard_cards (sort_order,title,subtitle,accent,metric_key,literal_value,is_active)
SELECT * FROM (
  SELECT 0 AS sort_order,'Total builds' AS title,'In catalog' AS subtitle,'rgba(120,255,180,0.4)' AS accent,'builds' AS metric_key,NULL AS literal_value,1 AS is_active
  UNION ALL SELECT 1,'Total orders','All time','rgba(80,180,255,0.4)','orders',NULL,1
  UNION ALL SELECT 2,'Registered users','Including admins','rgba(255,200,80,0.4)','users',NULL,1
  UNION ALL SELECT 3,'Revenue (delivered)','PHP (est.)','rgba(180,120,255,0.4)','revenue',NULL,1
  UNION ALL SELECT 4,'Pending orders','Needs action','rgba(255,120,120,0.35)','pending',NULL,1
) AS seed
WHERE NOT EXISTS (SELECT 1 FROM admin_dashboard_cards LIMIT 1);
