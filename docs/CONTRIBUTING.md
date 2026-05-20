# Contributing & publishing notes

This file is optional context for **forks** or **maintainers**. End users only need the main [README](../README.md).

## Publishing your fork to GitHub

From the repository root (the folder that contains `BeastPc.sln`):

```powershell
git init
git add .
git status
```

Confirm `git status` does **not** list `packages/`, `bin/`, `obj/`, `.vs/`, or `*.mp4` under `BeastPc/Content/video/` (those belong in `.gitignore`).

```powershell
git commit -m "Initial commit: BeastPC e-commerce (web frameworks project)"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPO.git
git push -u origin main
```

Create an empty repository on GitHub first (**New repository**), then add the remote URL that GitHub shows you.

## Pull requests

For coursework or portfolio forks, open a PR with a short description of what changed and how you tested it (e.g. F5 in Visual Studio, smoke test shop and admin).
