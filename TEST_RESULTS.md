# Clear Contrast v5 - 25 September 2026

- Release rebuild: 0 warnings, 0 errors.
- 29 screens at desktop/mobile sizes plus 10 retired routes: 68 checks, no failures or JavaScript errors.
- Browser computed styles verified: page rgb(223,227,217), report cards rgb(255,255,255), with the new shared card shadow.
- Desktop/mobile Reports previews reviewed. Shared styling covers dashboard, forms, lists, reports, staff/account panels and receipts.
- Compared application files to v4 ZIP: only site.css, manifest, offline page and the two receipt background styles changed. Business code and workflows are identical to v4.
- Updated previews included. Earlier release validation follows below.

# Easy Workflow v4 - Final Validation

- Final Release build passed with 0 warnings and 0 errors.
- 28 SQL Server checks passed, including sale/credit/payment/stock integrity, reversals, duplicate submissions, migration compatibility, correct dashboard payment totals and overdue follow-up.
- 29 end-to-end UX checks passed: customer business/phone search, product code search, visible stock/rates, keyboard entry, optional fields, rejected-sale data retention, all three sale types, full-balance shortcut, live list search, remembered filters, Khata actions and mobile search.
- 14 additional checks passed: touch selection, formatted phone search, unresolved-query protection, Escape recovery, staff access and eight viewport widths from 320 to 1920px.
- 29 screens on desktop/mobile plus 10 removed routes: 68 checks passed without page errors or horizontal page overflow.
- JavaScript syntax and literal page-navigation references passed.
- Desktop dashboard, sale form, payment form and mobile search previews inspected.

These tests used disposable sample data in CodexUXPreviewV4, not the agency database. No physical mobile-device, live-hosting or real-staff usability study was performed. Older evidence below is retained as release history.

# Validation - 24 September 2026

- Release application build: passed, 0 warnings, 0 errors (.NET SDK 9.0.318).
- Release verification project build: passed, 0 warnings, 0 errors.
- 25 real SQL Server LocalDB checks passed: migration compatibility, retained historical data, current model, customer edits, cash/credit/partial sales, balances, stock, duplicate submissions, overpayment, invalid payment method, rounding, reversals, cancellations, reports, Khata date filtering and protected deletion.
- 29 screens checked at desktop width 1440 and mobile width 390: successful responses, no horizontal page overflow, no JavaScript page errors.
- 10 retired page routes return 404; removed workflows are not just hidden.
- 16 browser workflow checks passed: three sale types, payment summary and saving, stock, customer search/Khata links, CSV exports, restricted staff access and mobile navigation.
- All literal Razor page navigation references resolve; encoding checks passed.
- Screenshot review covered login, dashboard, sale/payment forms and receipts, customers/Khata, products/stock, reports and staff screens.

These checks used an isolated test database and Microsoft Edge. They do not constitute testing on your live database, hosting server, physical phones or every browser.

## Repeat the core checks
On Windows with .NET 9 and SQL Server LocalDB:

```powershell
dotnet run --project Verification -c Release
```

The verification executable creates only MasterFeed_Clean_Verification_20260924 and refuses to run if it already exists. To remove that dedicated test database:

```powershell
dotnet run --project Verification -c Release -- --cleanup
```

The raw browser and financial-check results are in VerificationResults. Browser checks were run locally with temporary test accounts; those accounts and their credentials are not part of this package.

## Build Fix v2 verification
- Built a fresh extraction of the delivered source ZIP with the updated project file: 0 warnings, 0 errors.
- Added deliberately invalid C# and Razor fixtures at the retired Audit/Details, Audit/Index, Reports/UserActivity, Reports/Recovery, Cashbook/Close and AuditService paths in a separate test copy. Release build still passed: these stale files cannot enter compilation.
- Release publish of that test copy passed. Test fixtures are not included in the release archive.
- Original data-integrity and browser results above remain from the prior release; this patch changes project file inclusion and documentation only.

The user's exact 52-error workspace was not available. Reported filenames match retired modules, consistent with extracting the cleaned ZIP over an older folder. Fresh extraction is recommended even with the added compatibility guard.

## Warm Professional v3 verification
- Final Release build: 0 warnings, 0 errors.
- 29 screens at 1440px and 390px plus 10 retired routes: 68 checks, no failures or JavaScript errors.
- Dashboard status-card text containment/overlap checked at 24 effective viewport widths corresponding to desktop/mobile widths and 100%, 125%, 150%, 175% scaling: no failures. This tests layout widths; it does not claim hardware display calibration or Windows Night Light measurement.
- Screenshots visually reviewed for the desktop and mobile dashboard; updated desktop/mobile previews included.
- Core 25 verification checks passed using a separate CodexDesignPreviewV3 LocalDB instance; default LocalDB instance startup was unavailable during this session. No live agency database was modified.
- All literal page navigation references resolve. No business calculation changes in this design revision.
