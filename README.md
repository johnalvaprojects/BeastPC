# BeastPC

E-commerce site for **ready-to-ship gaming PCs**: browse builds, cart, checkout, user accounts, and an admin dashboard. Built with **ASP.NET MVC (.NET Framework 4.8.1)**, **MySQL**, and a custom dark UI.

**Author:** [johnalvaprojects](https://github.com/johnalvaprojects) · IT student · capstone / portfolio project

---

## Features

- Shop catalog with build detail panel and cart
- Checkout and order history (My Account)
- User registration, login, email verification flow
- Admin dashboard: manage PC builds, orders, KPI cards
- Responsive layouts: home, shop, about, contact

## Screenshots

Run the site locally with videos in `BeastPc/Content/video/` for the full look.

| Home | Login |
|:---:|:---:|
| ![Home page](docs/screenshots/home.png) | ![Login page](docs/screenshots/login.png) |

| Shop | Admin dashboard |
|:---:|:---:|
| ![Shop](docs/screenshots/shop.png) | ![Admin dashboard](docs/screenshots/admin.png) |

---

## Requirements

- **Windows** with **Visual Studio 2022** (ASP.NET and web development workload)
- **.NET Framework 4.8.1**
- **XAMPP** (or MySQL 8 / MariaDB) on port `3306`
- **HeidiSQL** (optional, for database import)
- **IIS Express** (included with Visual Studio)

---

## Local setup

1. **Clone** into your web root, e.g. `C:\xampp\htdocs\xampp\BeastPc\`

2. **MySQL**
   - Start MySQL in XAMPP.
   - Create database `beastpc`.
   - Export from your dev machine in HeidiSQL → **Tools → Export database as SQL** (save locally; do not commit files with real customer data).
   - On a new PC: **File → Run SQL file** and import that dump.
   - Or run scripts in `BeastPc/App_Data/sql/` if you are building schema from scratch.
   - See `BeastPc/App_Data/sql/README.md`.

3. **Connection string** — `BeastPc/Web.config` (default XAMPP):

   ```xml
   server=127.0.0.1;port=3306;database=beastpc;uid=root;pwd=;
   ```

   Change `pwd=` if your MySQL root has a password.

4. **NuGet**
   - Open `BeastPc.sln` in Visual Studio.
   - Right-click solution → **Restore NuGet Packages** (first time only; `packages/` is not in Git).

5. **Videos (required for full UI)**
   - Hero and nav backgrounds use MP4s under `BeastPc/Content/video/`.
   - These files are **gitignored** (too large for GitHub). Copy them from your original project folder or replace with your own clips:
     - `Content/video/videohomepage/` — `vid1.mp4` … `vid4.mp4`, `ultimategamingpc.mp4`
     - `Content/video/navbar/BlackTopoDesktop.mp4`

6. **Run**
   - Set startup project **BeastPc**.
   - Press **F5** (often `https://localhost:44301/`).

---

## Publish on GitHub

From the folder that contains `BeastPc.sln`:

```powershell
cd C:\xampp\htdocs\xampp\BeastPc
git init
git add .
git status
```

Confirm `git status` does **not** list `packages/`, `bin/`, `obj/`, `.vs/`, or `*.mp4` under `Content/video/`.

```powershell
git commit -m "Initial commit: BeastPC e-commerce capstone"
git branch -M main
git remote add origin https://github.com/johnalvaprojects/BeastPC.git
git push -u origin main
```

Create the empty repo on GitHub first: **New repository** → name `BeastPC` → no README (you already have one here).

---

## Project layout

| Path | Role |
|------|------|
| `BeastPc/` | MVC app (Controllers, Views, Content, Scripts) |
| `BeastPc/Controllers/BeastPcController.cs` | APIs: builds, orders, auth |
| `BeastPc/Views/Home/` | Shop, checkout, about |
| `BeastPc/Views/Admin/` | Admin dashboard |
| `BeastPc/App_Data/sql/` | SQL migrations |
| `packages/` | NuGet (restore locally, not in Git) |

---

## License

Academic / portfolio use. Add a license file if you publish publicly for employers.
