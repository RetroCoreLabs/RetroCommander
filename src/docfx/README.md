# DocFX site

The API reference for RetroCommander, built from the XML documentation in the code. It is
published to GitHub Pages by `.github/workflows/docs.yml` on every push to `main` that touches
`src/`.

Live site: https://retrocorelabs.github.io/RetroCommander/

## Build it locally

From the repository root:

```powershell
docfx src/docfx/docfx.json
```

Or from this folder: `docfx docfx.json`. Add `--serve` to look at it on http://localhost:8080/.
DocFX builds the two projects itself, so the RetroIO packages must be in the local feed that
`src/nuget.config` names.

## The files

| File | What it is |
|---|---|
| `docfx.json` | Which projects are documented and how the site is built. |
| `index.md` | The landing page. |
| `toc.yml` | The top menu. |
| `api/index.md` | The page above the API tree. Everything else in `api/` is generated. |
| `.gitignore` | Keeps the generated `_site/` and `api/*.yml` out of git. |
