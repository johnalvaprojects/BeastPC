# BeastPC database

## Local development (your machine)

1. Create database `beastpc` in HeidiSQL.
2. Export when needed: select `beastpc` → **Tools → Export database as SQL**.
3. Keep exports **local only** (`.gitignore` blocks `beastpc.sql` — may contain real users/orders).

## Scripts in this folder

| File | Purpose |
|------|---------|
| `001_add_username_to_users.sql` | Username column on `users` (older DBs) |
| `002_admin_dashboard_cards.sql` | Admin KPI cards table + seed |

The admin dashboard can also auto-create `admin_dashboard_cards` on first load.

## New clone / lab PC

1. Import your **private** SQL dump in HeidiSQL, **or**
2. Create empty `beastpc` and run `001` and `002` if your schema already matches the app.

Update `Web.config` connection string to match MySQL on that PC.
