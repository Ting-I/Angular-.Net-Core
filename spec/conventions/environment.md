# Environment

Machine and toolchain traps. `CLAUDE.md` carries the one-line fix for each; this file is why.

## `global.json` pins the .NET 9 SDK

SDK 10 is also installed on this machine and would be picked by default, targeting `net10.0`.
Leave the pin in place — removing it does not "modernise" the project, it silently retargets it.

## `node` may be missing from an agent shell's PATH

It *is* in the machine PATH (`C:\Program Files\nodejs\`); a shell that cannot see it inherited a
stale environment snapshot. Prefix the command:

```powershell
$env:Path = "C:\Program Files\nodejs;$env:Path"
```

Do **not** edit the system environment variable to "fix" this — the system one is already correct,
and changing it would be a machine-wide change to work around one shell.

## A running API locks the build output

`dotnet run` holds `CMS.API.exe`, so the next `dotnet build` or `dotnet test` fails with
**MSB3027** ("being used by another process"), after ten retries. Two ways out:

- Stop the running API, or
- send the build somewhere else: `dotnet test -p:OutDir=<a scratch directory>\`.

The second is the one to reach for when the API is running deliberately — it leaves the developer's
session alone. Pass the path with a trailing separator, and quote it in PowerShell.

## The connection string lives in one file

`src/CMS.API/appsettings.json` only — `Server=.\SQLEXPRESS;Database=CMS`.
`appsettings.Development.json` deliberately does not repeat it, so there is one place to change and
no chance of the two drifting.

## `npm test` needs its flags

Bare `npm test` enters watch mode and opens a browser, which never returns in a non-interactive
shell. Always:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless
```

The same flags are noted in `spec/conventions/testing.md`, where the rest of the test guidance is.
