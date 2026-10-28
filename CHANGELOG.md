# Änderungen

## Unveröffentlicht

- Neue Einstellung `area_code`: Ortsnummern ohne Vorwahl (ab fünf Ziffern, nicht mit 0 oder 1 beginnend) bekommen die Ortsvorwahl vorangestellt, z. B. `40254` → `076140254`. `install.ps1` fragt sie ab; `install.ps1 -AreaCode 0761` setzt sie in einer vorhandenen Konfiguration.

## 2.0.0 · 2026-10-28

- Neuanfang als kleines Windows-Programm für den T2med-Arbeitsplatz: `tel:`- und `callto:`-Links wählen per Action URI am Yealink-Tischtelefon.
- Kein Server, keine Weboberfläche, kein Python; Installation pro Windows-Benutzer mit `install.ps1`, Passwort des Telefons per DPAPI verschlüsselt.
- Übernimmt die Standard-App-Auswahl des bisherigen Clients und entfernt dessen Reste.
- Die frühere Middleware (Anrufererkennung, Patientensuche, GDT-Aktenaufruf, Weboberfläche) ist nicht mehr Teil des Projekts.
