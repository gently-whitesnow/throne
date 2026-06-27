# apps/landing

Public landing for Throne. Next.js App Router.

Deployed as a static export to the VPS via the `Deploy site (static)`
GitHub workflow (`.github/workflows/deploy-static.yml`).

## Design tokens

Colors are owned by the repo-root [`DESIGN.md`](../../DESIGN.md) — the same
canonical source `apps/web` reads. The landing consumes a generated CSS
palette built from it.

### Update flow

1. Edit color tokens in the root `DESIGN.md` frontmatter.
2. Regenerate the CSS palette:

   ```bash
   python apps/landing/scripts/sync_design_tokens.py
   ```

   This rewrites `apps/landing/src/app/styles/tokens.generated.css`.
3. Commit `DESIGN.md` and the regenerated CSS together.

### Drift check

```bash
python apps/landing/scripts/sync_design_tokens.py --check
```

Exits non-zero if the generated CSS is out of sync with `DESIGN.md`.

### Files

- `scripts/sync_design_tokens.py` — generator (color tokens only).
- `src/app/styles/tokens.generated.css` — generated palette, do not edit
  by hand.
- `src/app/styles/tokens.css` — non-color primitives (spacing, radii, font
  stacks, layout widths) plus an import of the generated palette.
