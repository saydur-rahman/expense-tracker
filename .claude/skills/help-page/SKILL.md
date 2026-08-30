---
name: help-page
description: Update the in-app Help page so it matches what the app actually does. Use after any change that alters what a user sees or does — a new or removed screen, a renamed control, a rule that behaves differently, an option that appears or disappears. Also use when asked to write, review, or reword the help/onboarding text.
---

# help-page

`src/frontend/src/pages/HelpPage.tsx` is the app's user-facing documentation, reached from
the **?** in the header (route `/help`). It is part of the product, not a doc folder — a
help page describing an app that no longer behaves that way is worse than none.

**The rule** (also recorded in `AGENTS.md`): a change that alters what a user sees or does
updates this page **in the same change**. A refactor, migration, or test-only change needs
nothing.

## When it needs an edit

Ask: *would a user who read this page yesterday now be wrong?*

| Change | Help page |
|---|---|
| New screen, or a screen removed | Yes — it walks through every screen in order |
| A control renamed, moved, or gone | Yes — it names controls literally ("press *Add*") |
| A rule that behaves differently (budgets, month cycle, archiving) | Yes — the *why* paragraphs explain these |
| An option that appears only for some accounts | Yes — say who sees it and who doesn't |
| Admin-only capability | Yes, in the `isAdmin` section |
| Refactor, migration, new test, perf work | No |

## How to edit it

- **Write for the user, not the codebase.** No entity names, endpoints, scopes, or service
  names. "Removing a category keeps its past spending" — never "soft delete via `IsArchived`".
- **Keep the shape.** A short "In short", then numbered `<Step>`s in the order someone
  actually meets the app (month cycle → categories → budgets → logging → dashboard), then
  the standing explanations (archiving, account), then the admin block.
- **Each `<Step>` names its screen** in the `where` prop, linked with `<Link>`. Add the link
  when you add a screen.
- **Explain the surprises.** The page earns its place on the rules people get wrong: heads
  can't exceed their category, budgets carry into the next month on their own, income takes
  no budget, removing keeps history, the month runs payday-to-payday. If a change adds a
  rule that would surprise someone, say so plainly and say *why*.
- **Admin-only content goes inside the `isAdmin` block**, so ordinary users never read about
  capabilities they don't have.
- Follow the repo's styling conventions — semantic surface tokens (`text-ink-soft`,
  `bg-card`), the `card` constant from `components/ui.ts`, no raw `gray-*`, no `dark:`
  variants on surfaces. Mobile-first.

## After editing

```bash
cd src/frontend && npx tsc --noEmit -p tsconfig.app.json
```

Then read the page in the running app (`run-dev` skill, the **?** in the header) — check it
on a narrow viewport, and check the admin block by signing in as an admin.
