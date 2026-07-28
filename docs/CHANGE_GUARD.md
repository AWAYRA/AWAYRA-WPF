# Change Guard Checklist

Use this checklist before declaring any Awayra task complete.

## Protected invariants

- [ ] Dashboard opens normally on first launch when Start Minimized is off
- [ ] Start minimized works only when explicitly enabled
- [ ] Tray Open restores the dashboard
- [ ] Settings opens visibly
- [ ] Close to Tray works
- [ ] Quit fully exits
- [ ] Only one effective instance and tray icon exist
- [ ] Eye Reset opens
- [ ] Move Break opens
- [ ] Overlays never overlap
- [ ] Settings persist
- [ ] Theme resources load without XamlParseException
- [ ] English, Persian, and Arabic remain available
- [ ] Scheduler behavior remains unchanged

## Required workflow

1. Inspect the requested scope
2. List the exact files allowed to change
3. Record protected behaviors that must remain unchanged
4. Run focused baseline tests before editing
5. Make the smallest possible patch
6. Inspect `git diff` after editing
7. Run focused tests
8. Run `scripts/verify-change.ps1` before declaring success
9. Never touch unrelated files
10. Never perform broad cleanup during a narrow fix
11. Never change UI while fixing backend behavior
12. Never change behavior while fixing visual styling
13. Never build Release or Installer unless explicitly requested
14. Never commit or push unless explicitly requested

## Verification command

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-change.ps1
```

A task is **BLOCKED** if regression verification fails.
