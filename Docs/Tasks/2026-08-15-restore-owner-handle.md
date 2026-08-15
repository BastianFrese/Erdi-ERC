#!/usr/bin/env pwsh
# Reparatur-Script für Sanitizer-Schaden vom 2026-08-15
# Ersetzt <OWNER_HANDLE> Platzhalter zurück auf den ursprünglichen Wert "Erdi"
# in 4 distinkten Kontexten. Trockenlauf via -DryRun.

param(
    [switch]$DryRun = $false
)

$ErrorActionPreference = 'Stop'

# Mapping: Pattern -> Ersetzung. Reihenfolge ist wichtig (längste zuerst).
$substitutions = @(
    @{ Pattern = '<OWNER_HANDLE>_ERC';  Replacement = 'Erdi_ERC';  Description = 'C# Root-Namespace (236 Files)' },
    @{ Pattern = '<OWNER_HANDLE>-ERC';  Replacement = 'Erdi-ERC';  Description = 'Folder-Name / UserAgent (z.B. appsettings.json)' },
    @{ Pattern = '<OWNER_HANDLE>10';    Replacement = 'Erdi10';    Description = 'Brand "Erdi10" (View/File-Name)' },
    @{ Pattern = '<OWNER_HANDLE>';      Replacement = 'Erdi';      Description = 'DisplayName / generischer Owner-Identifier' }
)

# BootstrapAdmin-DisplayName: zwei verschiedene Handles.
# Pattern: "DisplayName": "<OWNER_HANDLE>" — wir müssen die Reihenfolge kennen.
# Aus dem Pre-Sanitize-Stand (ltwiener, Erdi) war die Reihenfolge in appsettings.json:
#   [0] = ltwiener (du)
#   [1] = Erdi
# Wir ersetzen also nur den ZWEITEN Vorkommen in jedem File mit "Erdi", den ersten mit "ltwiener".

$totalReplacements = @{
    '<OWNER_HANDLE>_ERC' = 0
    '<OWNER_HANDLE>-ERC' = 0
    '<OWNER_HANDLE>10' = 0
    '<OWNER_HANDLE>' = 0  # DisplayName-Generisch
}

# Suche alle Files, die <OWNER_HANDLE> enthalten
$files = git grep -l '<OWNER_HANDLE>' 2>$null

if ($files.Count -eq 0) {
    Write-Host "Keine <OWNER_HANDLE>-Vorkommen gefunden. Repo ist sauber." -ForegroundColor Green
    exit 0
}

Write-Host "Gefunden: $($files.Count) Files mit <OWNER_HANDLE>" -ForegroundColor Cyan
Write-Host ""

foreach ($file in $files) {
    # Skip Binary-Files (MP4, XLSX) — die können wir nicht token-editieren
    $ext = [System.IO.Path]::GetExtension($file).ToLower()
    if ($ext -in @('.mp4', '.xlsx', '.xls', '.pdf', '.png', '.jpg', '.jpeg', '.gif', '.webp', '.ico')) {
        Write-Host "  SKIP (binary): $file" -ForegroundColor Yellow
        continue
    }

    $content = Get-Content -Raw -LiteralPath $file -Encoding UTF8
    $original = $content
    $fileHits = @{}

    # Pattern 1-3: einfache 1:1-Ersetzung
    foreach ($sub in $substitutions[0..2]) {
        $pat = $sub.Pattern
        $count = ([regex]::Matches($content, [regex]::Escape($pat))).Count
        if ($count -gt 0) {
            $fileHits[$pat] = $count
            $totalReplacements[$pat] += $count
            if (-not $DryRun) {
                $content = $content.Replace($pat, $sub.Replacement)
            }
        }
    }

    # Pattern 4: <OWNER_HANDLE> allein — unterscheide BootstrapAdmin-DisplayName-Kontext
    # Strategie: ersetze alle generischen <OWNER_HANDLE>, dann für appsettings.json
    # die Reihenfolge prüfen und den ersten mit "ltwiener" markieren.
    $genericHits = ([regex]::Matches($content, [regex]::Escape('<OWNER_HANDLE>'))).Count
    if ($genericHits -gt 0) {
        # Wenn appsettings(.Production)?.json: spezielles Handling
        $isAdminConfig = $file -match 'appsettings(\.Production)?\.json$'
        if ($isAdminConfig) {
            # In appsettings.json / appsettings.Production.json: zwei DisplayName-Einträge
            # Ersetze von hinten nach vorne, damit der erste "ltwiener" wird.
            $lines = $content -split "`n"
            $displayNameIndices = @()
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match '"DisplayName":\s*"<OWNER_HANDLE>"') {
                    $displayNameIndices += $i
                }
            }
            if ($displayNameIndices.Count -eq 2) {
                # Index 0 = ltwiener, Index 1 = Erdi
                $lines[$displayNameIndices[0]] = $lines[$displayNameIndices[0]] -replace '<OWNER_HANDLE>', 'ltwiener'
                $lines[$displayNameIndices[1]] = $lines[$displayNameIndices[1]] -replace '<OWNER_HANDLE>', 'Erdi'
                $content = $lines -join "`n"
                $fileHits['<OWNER_HANDLE> (admin:ltwiener,erdi)'] = 2
                $totalReplacements['<OWNER_HANDLE>'] += 2
            } else {
                # Fallback: alle generisch ersetzen
                $content = $content.Replace('<OWNER_HANDLE>', 'Erdi')
                $fileHits['<OWNER_HANDLE>'] = $genericHits
                $totalReplacements['<OWNER_HANDLE>'] += $genericHits
            }
        } else {
            $content = $content.Replace('<OWNER_HANDLE>', 'Erdi')
            $fileHits['<OWNER_HANDLE>'] = $genericHits
            $totalReplacements['<OWNER_HANDLE>'] += $genericHits
        }
    }

    if ($fileHits.Count -gt 0) {
        $hitsStr = ($fileHits.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', '
        if ($DryRun) {
            Write-Host "  DRY: $file -> $hitsStr" -ForegroundColor DarkGray
        } else {
            Set-Content -LiteralPath $file -Value $content -Encoding UTF8 -NoNewline
            Write-Host "  FIXED: $file -> $hitsStr" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "=== Zusammenfassung ===" -ForegroundColor Cyan
foreach ($key in $totalReplacements.Keys) {
    Write-Host ("  {0,-40} : {1}" -f $key, $totalReplacements[$key])
}

if ($DryRun) {
    Write-Host ""
    Write-Host "TROCKENLAUF — keine Files geändert. Entferne -DryRun zum Schreiben." -ForegroundColor Yellow
}
