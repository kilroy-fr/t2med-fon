# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

t2med-fon is a tiny Windows program on the T2med workstation: it is registered as handler for `tel:`/`callto:` links and sends the number via the Action URI (`/servlet?key=number=…`) to the workstation's Yealink desk phone. No server, no web UI.

User-facing text, docs, error messages and the CHANGELOG are in **German**; code comments are in English.

## Commands

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1         # builds dist\t2med-fon.exe, copies install.ps1 next to it
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test   # + script syntax check, tests\Tests.cs, install.ps1 -DryRun
```

There is no test filter; `tests\Tests.cs` is a plain assertion runner compiled together with `src\*.cs` (`/main:T2medFon.Tests`) and gets the built exe as argument for end-to-end runs. Tests only talk to the loopback `MockPhone`.

## Constraints

- Built with the `csc.exe` of .NET Framework 4.x that ships with Windows, so **C# 5 only**: no string interpolation, `?.`, `nameof`, expression-bodied members, auto-property initializers.
- `t2med-fon.exe` is `/target:winexe`: no console. Errors are shown as MessageBox; `--no-dialog` writes them to stderr (used by tests).
- `.ps1` files must be saved as UTF-8 **with BOM**, otherwise Windows PowerShell 5.1 garbles umlauts.
- `Number.FromLink` never guesses: anything but digits, a leading `+` and visual separators is rejected, so a link cannot inject other Action URI commands.
- The DPAPI entropy `t2med-fon` is shared by `Config.Entropy` and `install.ps1`.
- `install.ps1` keeps the ProgId `T2medFon.Url` and `HKCU\Software\T2medFon\Capabilities` from the 1.x client so an existing Windows default-app choice stays valid.

## Conventions

- On a release, the version appears in `src\Program.cs` (`Program.Version`), `README.md` and `CHANGELOG.md`. Unreleased changes go under `## Unveröffentlicht`.
- Tests use only fictitious numbers and loopback servers; never point them at a real phone.
