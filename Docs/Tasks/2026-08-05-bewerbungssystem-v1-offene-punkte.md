---
datum: 2026-08-05
tags: [task, bewerbung, rebuild, v1, offen]
status: [offen]
---

# Offene Punkte für den Bewerbungs-Rebuild V1

> **Kontext:** Begleitend zum Plan `<OWNER_HOME>\.claude\plans\twinkly-foraging-turing.md` (Status: in-arbeit). Diese Punkte müssen vor Commit 1 geklärt werden, damit das System konsistent umgesetzt werden kann. Morgen wieder aufgreifen.

---

## 1. ReviewNote-Sichtbarkeit für User

**Frage:** Soll der User in `/Application/MyApplication` den Admin-Reject-Grund (`Application.ReviewNote`) sehen?

**Status:** Noch nicht entschieden.

**Optionen:**
- **A) User sieht die Note** — maximale Transparenz. Passt zur bisherigen Erdi-Kultur (im alten `ApplicationForm`-System war ReviewNote schon für User sichtbar). Empfehlung des Plans.
- **B) Note ist Admin-intern** — User sieht keinen Reject-Grund. Macht härtere Reject-Notes möglich, ohne User zu verletzen. Falls intern notiert werden soll: separate `InternalNote`-Spalte.

**Was es für die Implementierung bedeutet:**
- A → `MyApplication.cshtml` rendert `ReviewNote` einfach in der Status-Karte.
- B → `MyApplication.cshtml` zeigt stattdessen einen generischen Hinweis ("Bitte wende dich an einen Admin, falls du Fragen hast."). Schema braucht ggf. `InternalNote`-Spalte.

---

## 2. ReviewNote im Webhook-Post-Bundle

**Frage:** Soll `ReviewNote` in den Webhook-Vars (`IWebhookAutomationService.FireAsync`) mit rein, die im Discord-Kanal landen?

**Status:** Noch nicht entschieden.

**Optionen:**
- **A) Note NICHT im Webhook** — Note bleibt in DB (Admin-Audit), landet aber nicht im Webhook-Vars-Bundle. Webhooks bleiben schlank. Empfehlung des Plans.
- **B) Note im Webhook-Bundle** — voller Audit-Trail im Discord-Kanal, inklusive Reject-Begründung. Macht Webhook-Posts länger.

**Was es für die Implementierung bedeutet:**
- A → `ApplicationService.AcceptAsync`/`RejectAsync` setzen `["ReviewNote"] = note` NICHT in den Vars-Dictionary.
- B → Vars-Dictionary enthält `["ReviewNote"] = note`. Webhook-Embed rendert die Note dann im Discord-Kanal.

---

## 3. Spaltenname: CreatedAt vs. SubmittedAt

**Frage:** Wie heißt die Zeitpunkt-Spalte in der neuen `Applications`-Tabelle?

**Status:** Noch nicht entschieden.

**Optionen:**
- **A) `CreatedAt`** — Konsistenz mit `League.CreatedAt`, `DriverProfile.CreatedAt`, `WaitlistEntry.CreatedAt` und allen anderen Entities im Projekt. Empfehlung des Plans.
- **B) `SubmittedAt`** — ausdrucksstärker für eine Bewerbung (Submit-Zeitpunkt). Inkonsistenz mit der Projekt-Convention.

**Was es für die Implementierung bedeutet:**
- A → `Application.CreatedAt` (mit `[DefaultValueSql("CURRENT_TIMESTAMP(6)")]` oder UTC-Default im Code).
- B → `Application.SubmittedAt`. `WaitlistEntry` aber bleibt bei `CreatedAt` → Inkonsistenz im selben Feature.

---

## 4. Discord-Invite-URLs: woher kommen sie?

**Frage:** Die neuen Felder `DiscordGuildOptions.CommunityInviteUrl` und `LeagueInviteUrl` — wo werden sie gepflegt?

**Status:** Noch nicht entschieden, aber sehr wahrscheinlich trivial.

**Wahrscheinliche Antwort:** Manuell in `appsettings.Development.json` (z. B. lokale Test-Invite-Links) + `appsettings.Production.json` (echte Erdi-Einladungen). Beide Dateien pflegen, sonst rendert das Warn-Partial ohne Links.

**Konkrete URLs werden zum Implementierungszeitpunkt gebraucht.** Vor Commit 5 (Views) nachfragen.

---

## 5. Wartelisten-Promotion-UI

**Frage:** Soll der Admin in `/AdminApplications/Waitlist` pro Eintrag einzeln promoten, oder per Bulk-Action über mehrere Einträge gleichzeitig?

**Status:** Noch nicht entschieden, aber trivial.

**Wahrscheinliche Antwort (Plan-Empfehlung):** Pro Eintrag einzeln, mit einem "Promoten zu Bewerbung"-Button pro Zeile. Einfacher zu implementieren, einfacher zu testen. Bulk-Action ist V2.

---

## 6. Capacity-Race-Condition: akzeptieren wir das Risiko?

**Frage:** Wenn zwei Bewerber parallel auf den letzten Stammfahrer-Platz submitten → werden beide angenommen (Liga hat 1 Stammfahrer zu viel). V2-Mitigation: Accept-Transaktion nochmal Capacity-Check. In V1: ignorieren?

**Status:** Noch nicht entschieden.

**Wahrscheinliche Antwort (Plan-Empfehlung):** In V1 ignorieren — Real-World-Szenario ist extrem unwahrscheinlich (Bewerber submitten manuell über Web-Form, nicht parallel in der gleichen Sekunde). Mitigation in V2 wenn nötig.

---

## 7. Reservefahrer-Lügen-Problem

**Frage:** User wählt im Formular "Reservefahrer", um den Capacity-Cap zu umgehen. Admin muss kontrollieren.

**Status:** Bewusst akzeptiertes Risiko.

**Wahrscheinliche Antwort:** Admin-Review fängt das ab. Falls häufig Problem: V2 ein Dropdown mit echtem Liga-Rollen-Scope (z. B. "Reserve für aktuelle Stammfahrer X"). Für V1: dokumentiert lassen.

---

## Erinnerungs-Anleitung für morgen

1. Lies zuerst `<OWNER_HOME>\.claude\plans\twinkly-foraging-turing.md` — der Plan-Stand.
2. Lies diese Datei — die offenen Punkte.
3. Kläre mit dem User die 3 Hauptpunkte (#1, #2, #3). Wenn der User es eilig hat: nimm die Plan-Empfehlungen (A/A/A).
4. Falls keine Klärung möglich: gleiche Defaults wie Plan-Empfehlungen, dokumentiere in der Memory-Datei `project_application_rebuild_v1_aug2026.md` als "Annahmen".
5. Erst dann Commit 1 (Models + Options + Webhook + Permissions) starten.