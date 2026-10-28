# t2med-fon

Ein Klick auf eine Telefonnummer in T2med wählt sie am Yealink-Tischtelefon des Arbeitsplatzes.

t2med-fon ist ein kleines Windows-Programm auf dem T2med-Arbeitsplatz. Es übernimmt `tel:`- und `callto:`-Links und schickt die Nummer per Action URI direkt an das Telefon. Server, Weboberfläche oder Python sind nicht nötig; das Programm läuft unter dem angemeldeten Windows-Benutzer ohne Administratorrechte.

Version 2.0.0 · [Änderungen](CHANGELOG.md) · MIT-Lizenz

## Voraussetzungen

- Windows 10 oder 11 (das enthaltene .NET Framework 4 genügt).
- Yealink-Tischtelefon im selben Netz, z. B. T46S.
- Telefon **und** Arbeitsplatz-PC mit fester IP-Adresse (DHCP-Reservierung im Router), weil das Telefon Wählbefehle nur von freigegebenen Adressen annimmt.
- Wird das Telefon vom Anbieter (z. B. fonial) automatisch provisioniert, beim Support klären, ob lokal gesetzte Action-URI-Einstellungen erhalten bleiben.

## Telefon einrichten

In der Weboberfläche des Telefons (`http://<Telefon-IP>`, Admin-Login). Menünamen können je nach Firmware leicht abweichen.

1. *Features → Remote Control* → *Action URI allow IP List*: IP-Adresse des Arbeitsplatz-PCs eintragen (Parameter `features.action_uri_limit_ip`). Nicht `any` eintragen.
2. Optional für Bluetooth-Headsets: *Features → General Information → Headset Prior* = Enabled, damit gewählte Gespräche am Headset landen.
3. Beim ersten Wählbefehl fragt das Telefon einmalig *Allow remote control?* – mit **OK** bestätigen.

## Installation auf dem Arbeitsplatz

Den Inhalt von `dist\` (`t2med-fon.exe`, `install.ps1`) auf den PC kopieren und als der Windows-Benutzer ausführen, der mit T2med arbeitet:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

Das Skript fragt die Ortsvorwahl der Praxis, die Adresse des Telefons (leitet die Weboberfläche des Telefons auf HTTPS um, `https://<Telefon-IP>` angeben; dann wird `tls_verify = false` gesetzt) und, falls das Telefon eine Anmeldung verlangt, Benutzer und Passwort der Telefon-Weboberfläche. Das Passwort wird mit Windows (DPAPI) für diesen Benutzer verschlüsselt gespeichert.

- Programm: `%LOCALAPPDATA%\t2med-fon\t2med-fon.exe`
- Konfiguration: `%APPDATA%\t2med-fon\t2med-fon.ini`

Danach unter *Einstellungen → Apps → Standard-Apps* nach **TEL** suchen und **t2med-fon** auswählen. War der frühere t2med-fon-Client bereits als Standard-App gewählt, gilt die Auswahl weiter; dessen Reste (Python-Helfer, Startmenü-Verknüpfung) entfernt das Skript.

Ein erneuter Aufruf aktualisiert das Programm und behält die Konfiguration; mit `-Phone http://…` wird sie neu angelegt, mit `-AreaCode 0761` nur die Ortsvorwahl gesetzt. Entfernen mit `-Uninstall`, Probelauf ohne Änderungen mit `-DryRun`.

## Prüfen

```powershell
& "$env:LOCALAPPDATA\t2med-fon\t2med-fon.exe" "tel:0761123456"
```

Das Telefon wählt sofort, ohne vorher selbst zu klingeln. Danach in T2med auf eine Telefonnummer klicken. Bei Problemen erscheint ein Fenster mit der Ursache:

| Meldung | Abhilfe |
| --- | --- |
| nimmt keine Wählbefehle von diesem PC an | IP des PCs in der *Action URI allow IP List* eintragen |
| hat die Anmeldung abgelehnt | Benutzer/Passwort prüfen: `install.ps1 -Phone http://<Telefon-IP>` |
| ist nicht erreichbar / antwortet nicht | Telefon-IP, Netzwerk, Telefon eingeschaltet? |
| leitet weiter | Telefon erwartet `https://`; dann meist auch `tls_verify = false` |
| Ungültige Rufnummer | Link enthält mehr als Ziffern, `+` und Trennzeichen |

## Rufnummern

Leerzeichen, Klammern, `-`, `/` und `.` werden entfernt. Internationale Nummern werden für die deutsche Wahl umgeschrieben: `+49 (0)761 123456` → `0761123456`, `+41 44 …` → `004144…`. Andere Zeichen führen zur Ablehnung.

Ist `area_code` eingetragen, bekommen Ortsnummern ohne Vorwahl sie vorangestellt: `40254` → `076140254`. Das gilt nur für Nummern ab vier Ziffern, die mit 2–9 beginnen; Nebenstellen (bis drei Ziffern) und Sondernummern wie `112` oder `116117` bleiben unverändert.

## Konfiguration

`%APPDATA%\t2med-fon\t2med-fon.ini`, eine Angabe pro Zeile, `#` leitet Kommentare ein:

| Eintrag | Standard | Bedeutung |
| --- | --- | --- |
| `phone` | – | Adresse des Telefons, z. B. `http://192.168.1.50` |
| `username` | leer | Benutzer der Telefon-Weboberfläche; leer = ohne Anmeldung |
| `password_dpapi` | – | vom Installer verschlüsseltes Passwort (alternativ `password` im Klartext) |
| `country_code` | `49` | eigene Landesvorwahl; leer = `+`-Nummern unverändert wählen |
| `area_code` | leer | Ortsvorwahl mit führender 0, z. B. `0761`, für Nummern ohne Vorwahl; leer = nicht ergänzen |
| `dial_path` | `/servlet?key=number={number}` | Wählpfad der Action URI, z. B. mit `&outgoing_uri=…` für ein bestimmtes Konto |
| `tls_verify` | `true` | `false` akzeptiert das selbst signierte Zertifikat des Telefons bei `https://` |
| `timeout_seconds` | `5` | Wartezeit auf das Telefon (1–60) |

## Entwicklung

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

Baut `dist\t2med-fon.exe` mit dem `csc.exe` des .NET Framework und prüft Rufnummern, Konfiguration und Wählbefehle gegen ein simuliertes Telefon auf `127.0.0.1`.

## Sicherheit

Das Telefon nimmt Wählbefehle nur von der freigegebenen PC-Adresse an; die Action URI ist unverschlüsselt (bzw. mit selbst signiertem Zertifikat), was nur im abgeschotteten Praxisnetz ausreicht. Die Nummer wird vor dem Senden auf Ziffern beschränkt, damit ein manipulierter Link keine anderen Telefonbefehle auslösen kann.
