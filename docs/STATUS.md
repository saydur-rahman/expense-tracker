# Project Status

**Last updated:** 2026-09-02

Read this first when picking the project back up. It records what is actually built and verified, what is not, and what to do next.

---

## Summary

The app was re-architected from a single API with embedded auth into **two independent services behind an OAuth 2.0 server**, orchestrated by .NET Aspire, on .NET 10.

Everything **builds, runs under Aspire, and has been verified end to end against live services** — including the full Authorization Code + PKCE flow, cross-service token validation, and every security boundary.

The main caveat is nearly unchanged: **almost none of the UI has been opened in a browser.** The exceptions, as of 2026-08-30, are the dashboard, the settings/profile screen (including the new password card) and the new help page — see those entries below.

---

## Architecture at a glance

| Piece | State |
|---|---|
| `Auth019` — OAuth2/OIDC server (OpenIddict 7), owns users | ✅ Working |
| `ExpenseTracker019.Api` — resource server, owns expense data | ✅ Working |
| `frontend` — React SPA, PKCE redirect flow | ⚠️ Builds & typechecks; **not browser-tested** |
| `ExpenseTracker019.AppHost` — Aspire orchestration | ✅ Working |
| Two separate databases, auto-migrated on startup | ✅ Working |

---

## What's verified

All of the following were exercised against **live running services**, not just compiled:

**OAuth 2.0 / OIDC**
- Discovery document and JWKS served correctly
- Full Authorization Code + PKCE flow: login → code → token exchange, returning access + refresh + id tokens
- Access token carries correct `sub`, `email`, `role`, `scope`, `aud`, `iss`
- Refresh-token grant works and **rotates** the refresh token (a reused one is rejected)
- The expense API — a *separate service* — validates Auth019-issued tokens via JWKS
- Unauthenticated requests are 401

**Impersonation (RFC 8693 token exchange)**
- Produces a token with `scope: expense.read` only, **no roles**, `imp_by` set, and **no refresh token**
- GET allowed; POST / PUT / DELETE all 403 with a clear message
- Cannot reach Auth019's admin API (403)
- Cannot be chained (an impersonated session cannot exchange again)
- Cannot target: yourself, another admin, or a deactivated user

**Deactivation**
- Blocks the `refresh_token` grant ("This account has been deactivated")
- Blocks the authorize endpoint
- Blocks impersonation of that user
- Admins cannot deactivate themselves
- Reactivation restores access

**Domain rules (regression-tested after the refactor)**
- Salary-cycle month resolution (`25 Jul – 24 Aug 2026`)
- Budget constraint: no category budget → rejected; exact match → allowed; over by 0.01 → rejected; lowering category below head sum → rejected
- Dashboard rollup arithmetic exact, over-budget flags correct
- Soft delete: archived head disappears from active lists but its spend stays in history **and** reports; new expenses against it are rejected
- 11 unit tests pass (month-cycle date math)

**Dates follow the user's clock, not the server's (fixed 2026-09-02)**
- Reported from New Zealand: "it keeps showing server time so I have to select everything manually". Two separate faults, both from treating UTC as a date
- **The form defaults were UTC.** `new Date().toISOString().slice(0, 10)` converts to UTC first, so at UTC+12/+13 every Expense and Income form opened pre-filled with **yesterday** for the whole working day, and had to be corrected by hand every time. Now `todayLocal()` in `lib/dates`, which reads the browser's own calendar
- **The server decided "today" from `DateTime.UtcNow`.** That is what resolves the current cycle, so on the 1st of a month a New Zealand user was shown **the previous month's** dashboard until about 1pm. New `IUserClock`, and the two sites in `MonthCycleService` now ask it
- **The zone travels as a header.** `api/client.ts` puts the browser's IANA zone (`Intl.DateTimeFormat().resolvedOptions().timeZone`) on **every** request as `X-Time-Zone`, so a new endpoint needing a date cannot forget to ask. Chosen over a stored profile field (no migration, nothing else to keep right) and over deriving it from `Country` (exact for New Zealand, a guess for the US or Russia). CORS already allowed any header
- **Anything missing or unrecognised falls back to UTC** — the behaviour we already had. A stale or spoofed header must never be a 500, and the value only affects which of the user's own periods is resolved
- IANA ids resolve on Windows as well as Linux, so the dev machine and the Linux App Service agree
- **Unit tests 27 → 43.** The New Zealand case is pinned exactly: 1 Sep 21:00 UTC is 2 Sep in Auckland, and 31 Aug 20:00 UTC is already 1 Sep — the month-boundary bug. Also daylight saving (Auckland is +12 in July, +13 in January, so a stored offset would be wrong half the year), a zone *behind* UTC, and every bad-header fallback
- Timestamps are untouched: `CreatedAtUtc` and friends stay `DateTime.UtcNow`. Recorded as a convention in `AGENTS.md`
- **Not yet confirmed in a browser** at the time of writing

**Registration form (added 2026-08-29)**
- Collects **mobile number**, **country**, and a **retyped password** alongside name and email
- Verified against the running service: mismatched passwords, a blank mobile, a malformed mobile, and a forged country code are each rejected with a field-level message
- A valid sign-up stores the mobile in Identity's existing `PhoneNumber` column and the ISO 3166-1 alpha-2 code in the new `Country` column
- The country list (244 entries) is derived from the runtime's globalization data in `Models/Countries.cs`, not a hand-kept list — **do not enable `InvariantGlobalization`** on Auth019 or the dropdown empties
- `AddUserCountry` adds a **nullable** column, so existing accounts keep `NULL` and nothing is backfilled

**Budget carry-forward (added 2026-08-30)**
- A new month is seeded with the most recent earlier month's category *and* head budgets, so figures set once keep applying
- Verified end to end against live services (15 checks, `dotnet test` + a scripted OAuth session): next month inherits; editing next month sticks and leaves this month alone; the month after inherits the *edited* figures; a month cleared on purpose stays empty across re-reads and re-resolves; past months are never back-filled; an archived category is not carried forward
- `BudgetsInitialized` on `BudgetPeriod` is what makes it run once per period — set on seeding **and** on every budget write

**Dashboard totals (added 2026-08-30)**
- A total-budget bar (spent vs left) and a Spent/Left donut with the remainder in green, above the per-category cards
- Colours were run through the data-viz validator against both card surfaces: `#6366f1` spent / `#16a34a` left pass the lightness band, chroma floor, colour-blind separation and 3:1 contrast in light **and** dark, so one pair serves both themes
- Over-budget flips both to red and the centre reads "Over"; a month with no budget shows a prompt instead of an empty ring
- The three old Budget/Spent/Left tiles were folded into this block rather than repeating the same figures twice

**Income ledger (added 2026-08-30)**
- `Category.Kind` splits categories into `Expense` and `Income` trees of the same shape; heads inherit the kind. Income never takes a budget
- New `Income` entity, `IncomeService`, `/api/incomes`, an **Income** page, and an Expense/Income switch on the Categories screen
- Dashboard gained an **Income vs expense** donut with the saved figure called out underneath, and **Expense/Income tabs** under it that switch the category breakdown
- Verified end to end against live services (23 checks): the two trees stay separate; the same name is allowed once per ledger; budgets on an income category or head are rejected; spending against an income head and income against a spending head are both rejected; totals (`totalIncome`, `totalSaved`) are exact and go negative when you outspend your income
- Carry-forward (15 checks) and the 11 unit tests re-run clean afterwards

**Currency and the settings screen (added 2026-08-30)**
- Currency is derived from the user's country and carried on the access token as `currency`; every amount in the app — dashboard, expenses, income, budgets — is formatted through one `useMoney()` hook
- New settings shell at `/settings` (sections down the left, content right; pills above the content on a phone), reached by clicking your name in the header. Sections: **Profile** and **Month cycle** (the old `/settings/month-cycle` URL still works)
- Profile edits name, mobile and country; **email is deliberately read-only**. Changing country triggers a silent token renew so the new currency applies immediately
- New `GET/PUT {auth}/api/profile` and `GET {auth}/api/profile/countries` on Auth019
- Verified against live services (21 checks): claims carry BD→BDT; the profile round-trips; an unknown country is rejected; a fresh token reflects a country change to US→USD; an impersonated session may read but **cannot** write (403), and the record is untouched afterwards; anonymous access is 401
- Accounts with no country (everyone created before 29 Aug, including `admin@example.com`) show plain numbers until a country is set on the Profile screen

**Profile completion is enforced before a token is issued (added 2026-08-30)**
- An external sign-up (Google) never sees the registration form, so it arrives with no mobile number and no country — and therefore no currency. New `Pages/Account/CompleteProfile` collects them
- Enforced in `AuthorizationController.Authorize()` rather than the external-login callback, so **holding a session cookie is not a way around it**: any account still missing a country is redirected there before a token is minted, then returned to the interrupted authorize request
- This also catches accounts created **before** the country field existed. `admin@example.com` and `victim@example.com` were given `BD` locally so the test scripts keep working; a real user is simply asked once
- Verified against live services (17 checks): authorize redirects and issues no code; the page renders 244 countries; a blank mobile and a forged country code are both rejected; completing it resumes the original authorize request and the resulting token carries `country` and `currency`; an already-complete account is not interrupted
- Regressions all clean afterwards: profile 21, income 23, month cycle 14, carry-forward 15, unit tests 11

**Changing your password from the Profile screen (added 2026-08-30)**
- The Profile screen gained a second card: **new password** and **retype password**, nothing else. There was previously no way to change a password at all once registered
- New `PUT {auth}/api/profile/password` on Auth019. It asks for **no current password** — the bearer token is the proof of identity
- **A Google-only account is not offered this at all.** `ProfileDto.hasPassword` drives it: the card is hidden when false, and the endpoint refuses with a 400 rather than trusting the client to have hidden it. It replaces a password, never grants one — a Google user's credential lives at Google. Linking Google to an account that already has a password leaves the password intact, so those users keep the card
- Internally a `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync` pair — the token is minted and spent inside the request, never handed out. Strength stays Identity's to judge, so a rejected password gets the same message registration would have given
- Impersonation is refused here exactly as on `PUT /api/profile` (rule 6); the guard is now one shared `ImpersonationBlocked()` helper covering both
- Verified against live services (14 checks): a freshly registered email/password account reports `hasPassword: true`; a mismatch, a blank and a too-short password are each rejected with their own message; a valid change returns 204; **the old password then fails to sign in and the new one succeeds**; an account with no password reports `hasPassword: false` and is refused with 400, gaining none; an impersonated session is refused with 403 and the password is untouched afterwards
- Changing the password moves the Identity security stamp, so the Auth019 **cookie** session dies within the 30-minute validation interval. The SPA keeps working — it renews on the refresh token, which OpenIddict does not stamp-check — but a fresh `authorize` asks for the new password. That is the wanted behaviour; don't "fix" it
- The Google-only case is exercised by nulling `PasswordHash` on a freshly registered account — the same state an external sign-up lands in — because Google credentials have never been configured locally. The UI branch (card hidden) follows from the same `hasPassword` flag the endpoint checks

**In-app Help page (added 2026-08-30)**
- New `/help` (`pages/HelpPage.tsx`), reached from a **?** in the header — deliberately not in the nav, which already carries five or six items on a phone
- Walks the app in the order someone meets it: month cycle → categories and heads → budgets → logging → dashboard, then the standing explanations (removing keeps history, currency, sign-in, password, feedback) and an **admin-only block** gated on `isAdmin`
- Written in the user's words, not the codebase's — no entity names, endpoints or scopes. It leads on the rules people get wrong: heads can't exceed their category, budgets carry forward on their own, income takes no budget, removing keeps history
- **This page is now part of every feature.** A change that alters what a user sees or does updates it in the same change — recorded as a convention in `AGENTS.md` and as the `help-page` skill, which carries the when/how and a table of what does and doesn't warrant an edit
- **Read in a real browser** (first UI screen in this project to be): the page renders end to end at desktop width, the header **?** highlights as the active route, every internal link resolves, and the admin block correctly does **not** appear for a non-admin
- Not yet seen: the **narrow/mobile** rendering (the extension's window resize reported success but never took effect — the page has no responsive branching of its own, so the risk is low) and the **admin block** rendered for an admin, which needs an admin sign-in

**Deep links survive sign-in (fixed 2026-08-30)**
- `CallbackPage` always navigated to `/`, and nothing stashed the route being attempted — so **every** deep link (`/help`, `/expenses`, `/settings/profile`, a bookmark, a link from an email) dumped the user on the dashboard after signing in. Found by navigating straight to `/help` in a fresh tab
- The attempted path now rides in the OIDC **`state`** parameter: `login()` sends `currentReturnPath()`, and the callback navigates to `safeReturnPath(user.state)`
- **`state` is validated, not trusted** — it round-trips through a URL and browser storage. Only a single-slash local path is accepted: `//host` and `/\host` are both browser-legal ways of leaving the site, and the auth routes themselves (`/callback`, `/silent-renew`, `/signed-out`) are excluded because landing back on one of them loops
- Verified in a browser: a fresh tab (empty `sessionStorage`, so a real sign-in round trip on the existing Auth019 cookie) opened at `/settings/profile` and at `/help` lands on **that** screen, not the dashboard

**Weekly budgets (added 2026-08-30)**
- A user now picks their **rhythm** in Settings → Budget cycle: **monthly** from a day of the month, or **weekly** from a day of the week. One rhythm at a time
- The storage model already suited this — a `BudgetPeriod` is just a start and an end date, and reports filter expenses by **date range**, not period id. So `BudgetService`, `ExpenseService`, `IncomeService`, `ReportService`, the dashboard and the budget constraint were **not touched at all**
- `PeriodKind` lands on both the setting row and `BudgetPeriod`. **It is part of a period's identity**: the unique index is now `(UserId, Kind, StartDate)` and every lookup filters on it, because a week and a month can start on the same day and the realign branch would otherwise rewrite one into the other. Carry-forward filters on it too, so a month's figure never lands in a week
- Switching rhythm leaves everything already budgeted untouched, and the first period on the new rhythm starts **unbudgeted** — a monthly figure sliced into weeks would be an amount the user never chose
- `MonthCycleMath` gained `ResolveWeekContaining`, `ShiftWeek` and `BuildWeekLabel`. The separate week label matters: `BuildLabel` shortens anything starting on the 1st to "Sep 2026", which for a week names a span five times too long
- **Bug caught only by running it: `HasDefaultValue` on an enum column silently discards the CLR default value.** EF treats such a property as store-generated and sends `DEFAULT`, so a user choosing `DayOfWeek.Sunday` (0) was written as the column default, Monday. Fixed with `ValueGeneratedNever()` on all three new enum columns — the column default still backfills old rows. A build and the unit tests were both green while this was broken
- Unit tests 11 → **27** (week resolution for all seven start days, month/year wrap, shifting, and every label form)
- Verified against live services (20 checks) with the cycle deliberately set so **the month and the week start on the same day**: both resolve as separate rows with their own labels, the first week is unbudgeted, the next week inherits the weekly figure and not the monthly one, and switching back finds the same month row with its end date and its budgets intact
- Seen in a browser: the Monthly/Weekly toggle, the day grid, the day-of-week list, and the warning shown only when the rhythm is actually being changed

**Heads drive the budget; the category figure is only a target (changed 2026-08-30)**
- **This replaces the original budget rule.** There is no ceiling any more. Put a figure on a head and it is accepted — no category budget needed first, and nothing caps it. A category's budget *is* what its heads add up to
- The figure stored on the category is a **target**: what you meant to spend. It never limits the heads and stands in as the budget only when **no** head is budgeted at all. Heads over or under it are reported as "200 extra" / "150 short", never refused
- Clearing the target no longer wipes the head budgets with it — that behaviour only made sense while heads lived inside the category's bounds
- The same rule had to land in **two** places: `BudgetService` for the editor and `ReportService` for the dashboard. They must agree; if the rule changes, change both
- Carry-forward now copies head budgets **independently** of category targets. Gated the old way, a user who only ever fills in heads would lose their entire budget at the turn of the period
- `CategoryBudgetDto` gained `target` and `difference` and lost `unallocated`; `amount` now means *the budget in force* rather than *the category row*
- **Existing data is unaffected in practice** — a category whose heads already sum to its budget reads identically. Where they don't, the category's budget now follows the heads
- Verified against live services (27 checks): a head budgeted with no category budget at all; heads never capped; a target under, over and equal to the head total; the dashboard measuring 1100 spent against the 1200 head total rather than the 1000 target; clearing the target leaving both head budgets intact; a target alone still working as the budget; and head-only budgets carrying into the next period
- Seen in a browser: heads directly editable, the heads total, the optional target, and the short/extra line

**Budget cards collapse (added 2026-08-30)**
- The Budgets screen's category cards now fold, matching the dashboard's, which already did. More than four and they start folded so the whole period fits on one screen; a search always opens its matches
- Collapsed, a card still says what matters: how many of its heads are budgeted, and whether the total matches the target

**Amount fields do arithmetic (added 2026-08-30)**
- Typing `635*3` in any amount box saves 1905, with the running total shown under the box as you type. Handles `+ - * /`, brackets, unary minus, `×`/`÷`, and grouping commas
- `lib/calc.ts` is a hand-written tokeniser and recursive-descent parser, **deliberately not `eval` or `new Function`** — the input is user text, and this way the grammar is exactly `+ - * / ( )` over numbers and there is no path to an identifier, a property access or a global
- `components/AmountField` is the shared input; `AmountHint` is exported separately for fields with their own layout, which is how the compact Budgets inputs show the same line. **New amount fields should use these rather than `Number(input)`** — recorded as a convention in `AGENTS.md`
- Wired into expenses, income, head budgets and category targets. An unreadable draft in a budget field is left on screen next to the message rather than silently discarded on blur
- `inputMode` on these fields moved from `decimal` to `text`. **This is a real trade-off:** an iOS decimal keypad offers digits and a separator only, with no way to reach `*`, so the arithmetic would have worked on desktop and not on the phone this app is built for. The cost is a full keyboard for plain numbers; pass `inputMode="decimal"` per field to take the old behaviour back
- Results settle to two decimals, matching `decimal(18,2)` storage — `10/3` is 3.33, and `0.1+0.2` is 0.3 rather than 0.30000000000000004
- Verified with **34 checks** against the evaluator, run through Node's type stripping rather than adding a frontend test runner: precedence, brackets, associativity, rounding, and rejection of letters, unbalanced brackets, double operators, division by zero, and `alert(1)` / `this.x` / `process`
- **Not yet seen in a browser** — the dev sign-in session had ended and signing back in needs the user's own password

**Income shown while budgeting (added 2026-08-30)**
- The Budgets screen leads with **income for that same period**, what is budgeted so far, and what is **left to budget**, so the decisions below it are made against a real figure
- Over-budgeting is shown, not prevented: the figure goes negative and turns red, with a line saying by how much. Refusing it would not make it less true, and people do genuinely plan past their income
- Scoped by the period, so it follows the cycle automatically — a weekly account sees that week's income, a monthly one that month's. The wording switches between "this week" and "this month" off `BudgetPeriod.Kind`
- `PeriodBudgetsDto` gained `totalIncome` and `totalBudgeted` rather than the screen making a second call to the reports endpoint. `totalBudgeted` sums each category's `amount`, so it follows the heads-first rule instead of re-deriving it
- Income under an **archived** head still counts — the query calls `IgnoreQueryFilters()` for the same reason the report queries do (rule 3)
- Verified against live services (14 checks): income appearing and totalling; left-to-budget falling as budgets are set; budgeting past income accepted and going negative; income dated outside the period excluded; and on a weekly cycle, income from earlier the same month but before the week began correctly left out, then counted again on switching back to monthly

**Personal loans and investments, and a menu that can grow (added 2026-09-02)**
- Two new screens, both **views over the ledgers rather than a third ledger**. You already logged a repayment as an expense; a loan turns those expenses into "you owe 11,500 and here is every payment"
- **Balances are computed on read, never stored** — a SUM over the expenses on the linked heads. Storing a running total would have meant `ExpenseService`'s create, update *and* delete paths maintaining it, and it would drift the first time an old row was edited. Recorded as rule 9 in `AGENTS.md`
- **Every expense on a linked head counts**, with no per-expense tagging. That is what forces the other rule: **a head belongs to at most one loan and one investment**, enforced by a unique index on `LoanHead.HeadId` / `InvestmentHead.HeadId`, because two loans sharing a head would each count the same payment. Payments are floored at the loan's `TakenOn`
- A loan is a name, a lender, the amount borrowed, a date and a remark. **No interest, no principal source, no ledger entry** — borrowing is not earnings, so it never touches income or the budget-vs-income ladder
- An investment carries **no amount at all**: both sides derived. Contributions are expenses on its `Expense` heads, returns are income on its `Income` heads — two links against two kinds, which stays inside rule 8 rather than adding a third `CategoryKind`
- Rings mirror each other: wholly red, filling green as a loan is repaid or an investment's capital comes back, with the figure in the centre. Per-cycle column charts and the latest 20 transactions underneath
- **`DateRangePicker` is the first place the app's date scope is not the shared cycle.** Dashboard, Budgets, Expenses and Income agree on the month or week on purpose; a loan outlives any single one. Scoped to these two screens and recorded in `AGENTS.md`
- **Menu reshaped.** The bar was full at five items (six as admin) and this would have made eight. Dashboard / Expenses / Income stay; everything else is behind **More** — a dropdown on desktop, a bottom sheet on a phone. The sheet renders through a **portal**: the bottom bar carries `backdrop-blur`, which makes it the containing block for any fixed descendant, so the sheet would otherwise have been trapped inside a 48px strip. More shows as active while you are on one of its screens, or Budgets appears to have vanished
- **Chart primitives extracted** to `components/charts/` (`TwoSliceDonut` with an optional centre, `Bar`, `ProgressBar`, `LegendRow`, the new `PeriodBars`, and the validated marks in `colors.ts`). They were private functions inside `DashboardPage` and `PeriodOverview`; the dashboard is unchanged by the move. Still no charting library, and there should not be one
- `LoanMath` is a pure static class beside `MonthCycleMath` and is unit-tested: **unit tests 27 → 44**. Both features run through it — an investment's payback is the same sum as a loan's balance
- **The migration adds four tables and touches nothing existing** — no columns added to `Expenses`, `Incomes` or `Heads`, so no backfill and none of the default-value traps
- Verified against live services in a browser: the More dropdown; creating a loan with a linked head; the ring wholly red at 12,000; logging 500 on the linked head dropping it to 11,500 with the per-cycle column appearing; and **deleting that expense returning the loan to 12,000** — the case a stored balance would have got wrong
- Still unseen: the **phone** More sheet (the extension's window resize reports success but never takes effect — a long-standing quirk here) and **dark mode**. Also unexercised: several heads on one loan, the 409 on a shared head, and the investment screens with real data

**Frontend toolchain brought current (2026-09-01)**
- **TypeScript 6 → 7.0.2** — the Go-native compiler, and a stable `latest`, not a preview. It went in with **zero source changes**: `tsc -b` and `tsc --noEmit -p` behave the same, the tsconfigs needed nothing, and oxlint is unaffected (it never used tsc). The typecheck is visibly faster
- **`@types/node` 24 → 26.4.0.** Worth knowing: this is one major *ahead* of the installed runtime (Node v25.9.0), so it types Node 26 APIs that would throw here if anything called them. In this package `@types/node` only covers `vite.config.ts`, so the exposure is nil — but if that ever stops being true, `@types/node@25.9.5` is the version that matches the runtime
- Everything else was already current and took in-range patch bumps. `npm outdated` is now empty
- **`"strict": true` added to both tsconfigs.** It had never been set, so `strictNullChecks` and `noImplicitAny` were **off** across the whole frontend — every `?.` and `!` in the codebase was decoration rather than an enforced check. Turning it on produced **zero errors**: the code was already strict-clean by habit, and is now held that way. This was free, and it is the kind of thing that only stays free if you do it early
- Node itself (v25.9.0), npm (11.12.1) and .NET (10.0.400) were already latest
- Verified after: frontend typecheck, production build, oxlint (same six pre-existing warnings, no new ones), 27 unit tests, the 59 ladder checks, and the running app reloaded clean in a browser with no console errors

**Always-on period overview: four bars on one scale (added 2026-09-01, reshaped 2026-09-02)**
- The dashboard's money figures used to live *inside* the Expense/Income tabs, so you only ever saw one half. Four bars — **Budget, Income, Spent, Left** — now sit **above** the tabs and do not move when you switch them
- **The budget is the measuring stick.** All four bars share one scale (the largest of the four figures) and a budget line is drawn at the same point on every one of them, **over** the fills — so a bar that beats the budget is seen crossing it, and one that falls short is seen falling short. Nothing is clipped and no bar is pinned to full width
- **Budget bar**: red while it exceeds income, green once income covers it. **Income bar**: the ladder — red short of budget, green at it, yellow 20% clear, gold 50% clear. Income accrues through the period while the budget is set once, so the bar grows and climbs rungs as the period goes on; red on the 2nd is information, not a failure, and the help page says so
- **Spent and Left are deliberately static** — brand blue and a muted slate — so the two bars above are where colour means something. The one exception is **Left going negative** (you outspent your income): it draws a red stub whose length is the size of the overspend, and the figure reads below zero
- **Left is income minus spending**, not budget minus spending: money actually still in hand. It carries a caption saying so, because the dashboard already had a "Left" meaning the other thing
- **This is a fourth and fifth colour in a deliberately three-colour design.** `surplus-*` (yellow) and `gold-*` are declared in `index.css` next to the others and recorded in `AGENTS.md`, scoped to this one ladder. As text they only ever appear as **tinted pills** — yellow cannot reach 4.5:1 on a light card — each with a glyph (▲ / ★) so the rungs are separable without hue
- Thresholds live in one list in `lib/budgetHealth.ts` as whole percents, apart from the component that draws them. Adding a rung is one line there plus a fill colour and a sentence
- **Compared in whole cents, not as a floating ratio.** `income / budget >= 1.5` put a figure that is *exactly* 50% clear a hair under the rung it earned — 123,456.78 against 185,185.17 did precisely that. Caught by the check script, not by the build
- **One source of truth:** both the dashboard and the Budgets screen feed the bars from `/api/reports/summary` on the same query key, so they cannot disagree. `PeriodBudgetsDto.TotalIncome`/`TotalBudgeted` (added 2026-08-30 for the Budgets screen) are **removed** — they were computed over a slightly different set of categories than `ReportService` uses, which is how the same block would have printed two different budgets on two screens. Budget and category writes now invalidate `['summary']` too
- The Budgets screen's old Income / Budgeted / Left-to-budget block is gone; nothing it said is lost — left-to-budget is the gap between the Budget and Income bars
- Figures the bars now own were trimmed from the tab cards rather than printed twice: the "Total budget" and "Total income" headlines, and the closing "Saved X this month" sentence
- Verified: 27 unit tests, `dotnet build` clean, frontend typecheck, oxlint and production build clean, and **59 scripted checks** over the ladder (every rung boundary to the cent, at six magnitudes; no budget; negative budget; no NaN or Infinity; exact money deviations) run through Node's type stripping, as `lib/calc` was. Tailwind was confirmed to emit every new fill class including the `bg-ink/45` budget line
- **Seen in a real browser**, driving the app end to end against the live stack. Every state walked against a 2,400 budget: income 0 (both top bars red, income bar empty); income 1,200 (both red, income bar visibly short of the line); income 3,600 with nothing spent (budget bar green ending exactly at the line, income bar gold crossing it, ★ pill reading "50% clear of your budget"); and spending 4,000 against 3,600 income (Spent blue and now the longest bar, crossing the line; **Left** a short red stub reading −400). Earlier, before the reshape, each rung was also confirmed at its exact boundary — green at exactly 2,400, yellow at exactly 2,880, gold at exactly 3,600
- The block held still across the Expense/Income tabs, and the dashboard and Budgets screen printed identical figures on the same period. Editing a head budget on the Budgets screen moved the bars **live** and dropped the rung gold → green, which is the `['summary']` invalidation working
- Still unseen: the **narrow/mobile** rendering (the extension's window resize reports success but does not take effect — the same quirk recorded for the help page) and **dark mode**, since the pills' and bars' dark values were never rendered. Both are worth a look on a real phone and a dark-themed browser

**Browsing earlier cycles from every screen (added 2026-08-30)**
- Two separate faults, not one. The **dashboard** was hardcoded to `reports/summary/current` with no picker at all. **Expenses and income** sent no date filter, so the API returned *all* history — but `pageSize` defaults to 25 and the UI had no pager, so only the newest 25 rows were ever reachable. That was the "can't load all the previous values"
- `PeriodPicker` now appears on the dashboard, expenses, income and budgets, so all four agree on which cycle is being shown. The arrows step; the label is a dropdown that jumps
- New **`GET /api/budget-periods/recent`** returns *computed* cycle windows — `{offset, kind, startDate, endDate, label}` — back to the user's earliest expense, income or stored period, capped at 240
- **Two traps this design exists to avoid.** A dropdown built from the `BudgetPeriods` table would silently skip cycles: rows are created lazily on first visit, so a month holding expenses but never opened has no row. And it would mix kinds — a user who tried Weekly has week rows sitting among the months. Computing from the current cycle fixes both
- **Listing history must not write.** `relative/{offset}` creates the row and runs carry-forward, so resolving a list of periods would create them wholesale and could carry budgets into months never budgeted. `recent` computes and persists nothing — now a convention in `AGENTS.md`
- Expenses and income use `useInfiniteQuery` with a **Load more** button. A single cumulative request was tried first and rejected: the API caps a page at 100, so it would have quietly stopped loading once a period passed that — the same class of bug being fixed
- Verified against live services (20 checks): a fresh account showing one window; history reaching back four months with contiguous offsets and no gaps; **listing creating no rows**; more windows listed than rows stored; an old expense reachable through its own cycle and absent from the current one; 30 rows paging cleanly as 25 + 5 with no duplicates; and switching to weekly relisting as weeks with no months mixed in
- The help page's claim that these screens showed "the current period" was wrong before this — they showed everything, first page only. Corrected

**Logout actually ends the session (fixed 2026-08-30)**
Three separate defects, found by reproducing the reported "it keeps coming back as the previous session":
- **The landing page was protected.** `post_logout_redirect_uri` was `/`, which sits inside `ProtectedRoute` — so signing out immediately started a *new* sign-in. New public `/signed-out` page; the URI is registered in `AuthSeeder` alongside the old one
- **Google silently re-authenticated**, which is why it came back as the *same* account. This turned out to be a symptom of the point above, not a defect: `prompt=select_account` was tried as a mitigation and then **removed** on 2026-08-30 — someone already signed in to Google should go straight through, and Google shows its own chooser when several accounts are signed in. Don't re-add it
- **The refresh token survived logout** — a real security bug, not a symptom. Dropping the cookie left every issued refresh token redeemable, so anything holding one could keep minting access tokens after the user logged out. `Logout()` now revokes the user's tokens via `IOpenIddictTokenManager`, the same way deactivation does
- Verified against live services: the cookie is cleared, the next authorize demands a fresh login, the refresh token is rejected, `prompt=select_account` is sent, and `/signed-out` renders
- **Note:** logout now revokes that user's tokens everywhere, so it signs them out on their other devices too

**Feedback with admin triage (added 2026-08-30)**
- Users send feedback from **Settings → Feedback** and see the whole conversation there; admins work it from **Admin → Feedback** with an Open / In progress / Resolved filter
- A thread is a list of messages, opening message included, so one component renders it for both sides
- Statuses: `Open` → `InProgress` (set automatically when an admin first replies) → `Resolved`. **Resolved closes it to everyone**, enforced in `FeedbackService` so neither controller can bypass it; an admin can reopen by setting the status back
- Admin endpoints require the **Admin role**, which impersonation tokens never carry
- The submitter's name and email are snapshotted onto the row — a deliberate denormalisation, since this service owns no user table and can't join across the service boundary
- Verified against live services (24 checks): submit, list, ownership isolation (another user gets 404), non-admin gets 403, admin reply auto-advances the status, both sides' replies are rejected once resolved and the thread length is unchanged, reopening restores replies, and the status filter works
- Regressions clean: income 23, carry-forward 15, month cycle 14, complete-profile 17, profile 21, unit tests 11

**Month cycle now re-cuts on the spot (fixed 2026-08-30)**
- Changing the cycle start day used to do nothing until the next month: resolution preferred any stored period already covering today. Boundaries are now always computed from the current setting, with the stored row used only as the budget anchor
- Verified against live services (14 checks): 1st → 25th → 15th each re-cut the current month immediately; the dashboard summary follows; previous/next step by the new cycle; and going **back** to the original start day returns the original period row with its 9,000 budget intact
- Carry-forward (15) and income (23) suites re-run clean afterwards

**Dashboard reshaped (2026-08-30)**
- Expense/Income tabs moved to the **top** and now switch the whole view — the Expense tab shows the budget bar and Spent/Left donut with the spending breakdown; the Income tab shows total income, the Income vs Expense donut, the saved figure and the income breakdown
- The donut is thinner (10px stroke, was 16) with an **open centre** — the figures were colliding in the hole, and they already appear in the legend beside it

**Design pass — red / green / blue (2026-08-30)**
- Three colours now carry meaning everywhere: **blue** = brand (actions, links, selection), **green** = money you still have (budget left, income, savings), **red** = trouble (over budget, overspent, destructive actions). Indigo is gone
- Tokens live in `src/frontend/src/index.css` under Tailwind v4's `@theme` as `brand-*`, `positive-*`, `negative-*` — use those, not raw `indigo-*`/`red-*`/`green-*`
- Shared pieces: `components/Button.tsx` (primary / secondary / ghost / danger × sm / md / lg) and `components/ui.ts` (card, emptyState, field, fieldSm, eyebrow, pageTitle)
- Chrome reworked: sticky blurred header with a brand mark, underline nav on desktop, a top-bar marker on the mobile tab bar, softer `rounded-xl` cards with `shadow-sm`, consistent focus rings, and Auth019's sign-in/register pages moved onto the same palette
- The chart mark changed from indigo `#6366f1` to brand blue `#2563eb`; **re-validated with the data-viz validator** against both card surfaces — blue/green passes lightness band, chroma, colour-blind separation and 3:1 contrast in light and dark
- The impersonation banner is now solid red rather than amber: it is deliberately the loudest thing on screen and stays inside the three-colour scheme

**Tinted surfaces, collapsible categories, searchable pickers (2026-08-30)**
- Nothing is pure white or black any more. Surface roles (`page`, `card`, `raised`, `input`, `track`, `line`, `line-soft`, `ink`, `ink-soft`, `ink-muted`) are declared as CSS custom properties and mapped through Tailwind v4 `@theme inline`, so `bg-card` / `text-ink` / `border-line` resolve per theme **without any `dark:` variant** — roughly 200 of them were deleted
- Ground is a soft blue-grey (`#eef2f7` light, `#0b1220` dark) with panels just above it; Auth019's pages use the same values
- Chart marks **re-validated** against the new card surfaces (`#f8fafc` / `#141d2e`) — blue/green still passes every check in both modes
- Dashboard category cards are **collapsible**, showing name, total and progress collapsed, heads when open. They start open when there are ≤ 4 categories and collapsed beyond that
- New `components/SearchableSelect.tsx` — a type-to-filter combobox (keyboard driven, matches on head *or* category name) replacing the native head pickers on Expenses and Income, for both the entry form and the list filter
- Budgets and Categories gained a search box that filters by category or head name, appearing once there are more than 4 categories

**Azure deployment, free tier (added 2026-08-30)**
- `infra/main.bicep` — one free Azure SQL database, an F1 Free Linux App Service plan carrying both APIs, and a Free Static Web App for the SPA. **Compiles clean** (`az bicep build`), 7 resources, 7 outputs
- `.github/workflows/deploy.yml` — merge to `main` runs tests, deploys infrastructure, publishes both APIs, builds the SPA against the real hostnames, uploads it, and smoke-tests (waits for the discovery document; asserts the API 401s anonymously). Signs in with federated OIDC, so no Azure client secret is stored
- **Azure allows one free SQL database per subscription**, so both services share it: Auth019 moved to the `auth` schema (migration `MoveAuthToOwnSchema`) with its own `auth.__EFMigrationsHistory`; the expense API keeps `dbo`. They still share no tables. Splitting later is a connection-string change, not a code change
- Auth019 can now load its OpenIddict signing certificate from a **base64 PFX app setting** — free hosting has no certificate store. With none configured it falls back to ephemeral keys and warns loudly: tokens then die on every restart
- Full setup — resource group, federated credentials, repo secrets, certificate generation — is in [DEPLOY.md](DEPLOY.md)
- **Deployed and running.** First shipped 2026-08-30; ten successful runs of the workflow that day, the last of them merge #4. `AZURE_RESOURCE_GROUP`, `AZURE_NAME_PREFIX` and `CUSTOM_DOMAIN` are set as repo variables, and all ten secrets (federated Azure credentials, SQL and admin-seed passwords, the OpenIddict certificate, Google credentials) are configured against a `production` environment

**Aspire orchestration**
- All six resources reach Running: `sql`, `auth019db`, `expensedb`, `auth019`, `expenseapi`, `web`
- Dependency ordering honoured (`Waiting → Running`)
- Frontend served through Aspire's proxy at :5173
- Full PKCE flow + impersonation verified *through the orchestrated stack*, not just standalone

---

## Known gaps

Ordered by how much they should worry you.

### 1. The UI has never been rendered in a browser
Everything frontend-side is verified by TypeScript compilation, a clean production build, and the fact that every API it calls is individually tested — **not** by looking at it. The OAuth redirect flow in particular (`/callback`, silent renew, the impersonation banner) has only been exercised at the protocol level via scripts.

**This now also covers the dashboard donuts, total bar, ledger tabs, the Income page, and the settings/profile screens (added 2026-08-30).** Their arithmetic is verified and the palette was validated by script, but the geometry — arc gaps, the centred figure, how two donuts plus tabs stack on a phone — has never been looked at. Check the empty-budget, part-spent, fully-spent and over-budget states, the no-income state, and the now five-item bottom nav on a narrow screen.

**Do this first:** run the AppHost, open http://localhost:5173, and walk through sign-in → month cycle → categories → budgets → expense → dashboard, then the admin screen and "View as". Check a phone viewport too.

### 2. Google sign-in is unproven
Auth019 wires Google as an Identity external provider and the login page shows the button when configured, but no real credentials were ever set, so the path has never executed. Set `Google:ClientId` and `Google:ClientSecret` in Auth019's user-secrets to enable it. Email/password is fully working and independent.

### 3. Production OAuth signing keys — closed
Development still uses OpenIddict's ephemeral dev certificates, but production now loads a real certificate from the `OPENIDDICT_CERT_BASE64` / `OPENIDDICT_CERT_PASSWORD` secrets, and has done since the first deploy on 2026-08-30. What is *not* recorded anywhere is the certificate's expiry — worth checking, because tokens die when it lapses.

### 4. Mobile polish pass not done
Layouts are mobile-first with a bottom tab bar, but no dedicated pass happened. Loading and error states are minimal.

### 5. Test coverage is narrow
Only `MonthCycleMath` has unit tests. The budget constraint and the impersonation/scope rules are verified by scripted live calls, not automated tests. Closing that gap is the highest-value testing work: `BudgetService` is pure service logic and easy to test.

### 6. Deployment — closed
Shipped on 2026-08-30 and green every run since. Merging to `main` is what deploys; nothing else does. **These gap notes had said "never deployed" for three days after it had been — check the workflow run list before trusting this section again.**

### 7. Mobile number and country are collected but never shown
Registration stores both, but nothing surfaces them afterwards — `AdminUserDto` doesn't carry them, there's no profile screen, and no one can correct a typo in their own number. Neither field is validated beyond shape: the mobile is not uniqueness-checked and not verified by SMS.

### 8. `seed-data` skill is a stub
No seeding command exists yet; test data means clicking through the UI or calling the API.

---

## Suggested next steps

1. **Browser-test the whole flow** (gap 1) — highest value, cheapest.
2. **Add unit tests for `BudgetService`** covering the constraint edge cases (gap 5).
3. **Mobile polish pass** (gap 4).
4. **Wire up Google sign-in** when you have credentials (gap 2).
5. ~~Deployment~~ — done. It ships to Azure App Service + Static Web Apps on merge to `main`, not the container hosting this line used to propose. See [DEPLOY.md](DEPLOY.md).

---

## Environment notes

- **Docker Desktop must be running** — Aspire starts SQL Server in a container.
- Service ports are stable, taken from each project's `launchSettings.json`: Auth019 on **5068**, the expense API on **5089**, the frontend on **5173**. Aspire's own dashboard port varies — read it from the AppHost console. (Auth019's fixed port is what makes an OAuth redirect URI like `http://localhost:5068/signin-google` safe to register.)
- Secrets live in .NET user-secrets, never the repo. Auth019: `AdminSeed:Email`, `AdminSeed:Password`, optionally `Google:ClientId`/`Google:ClientSecret`.
- Local dev accounts (throwaway): `admin@example.com` / `Password123!` (Admin), `victim@example.com` / `Password123!` (plain user).
- On Windows use PowerShell `Invoke-RestMethod` rather than `curl` for API testing. `sqlcmd` needs `-I` to `UPDATE` tables with filtered indexes — without it the statement fails while appearing to succeed.

---

## Bugs found and fixed during the build

Recorded because they are the kind that recur.

**Migration would have locked out every existing user.** Adding the non-nullable `IsActive` column backfilled existing rows with SQL's `false` default rather than the C# `= true` initializer. Fixed with `HasDefaultValue(true)`. **Any future non-nullable column with a C# default needs the same treatment.**

**`/me` returned 404 for valid tokens** (pre-OAuth architecture). ASP.NET Core remaps `sub` to a long `ClaimTypes.*` URI by default.

**Token exchange rejected every request.** The handler authenticated with the *validation* scheme, which looks for an `Authorization` header — but a token-exchange request carries the subject token in the form body. Fixed by authenticating with the *server* scheme, which exposes the principal OpenIddict already resolved.

**Token exchange registered as a custom flow.** OpenIddict 7 supports RFC 8693 natively, so `AllowCustomFlow(...)` throws; `AllowTokenExchangeFlow()` is correct.

**Issuer mismatch under Aspire.** Auth019 derived `iss` from the proxy host while the API was configured with a different internal address, so every cross-service call was 401. Fixed by pinning one issuer in `AppHost` and passing it to both.

**Vite ignored Aspire's allocated port**, drifting to 5174 while the proxy pointed at 5173, so the frontend appeared to hang. Fixed with `port: process.env.PORT` + `strictPort`.
