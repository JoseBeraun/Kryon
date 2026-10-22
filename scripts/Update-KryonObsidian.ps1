param(
    [string]$Feature = "001-gestion-usuarios"
)

$ErrorActionPreference = "Stop"

$Repo = Split-Path $PSScriptRoot -Parent
Set-Location $Repo

$TasksFile = "specs\$Feature\tasks.md"
$ControlFile = "docs\kryon\00-Control-Center.md"
$DashboardFile = "docs\kryon\05-Dashboard-Automatico.md"

if (-not (Test-Path $TasksFile)) {
    throw "No existe: $TasksFile"
}

if (-not (Test-Path $ControlFile)) {
    throw "No existe: $ControlFile"
}

# -----------------------------
# GIT
# -----------------------------

$Branch = (git branch --show-current).Trim()
$Hash = (git rev-parse --short HEAD).Trim()
$Commit = (git log -1 --pretty=%s).Trim()

$Status = @(
    git status --short |
    Where-Object { $_ -notmatch '\.obsidian' }
)

if ($Status.Count -eq 0) {
    $StatusMd = "✅ Working tree limpio."
}
else {
    $StatusMd = ($Status | ForEach-Object { "- ``$_``" }) -join "`r`n"
}

# -----------------------------
# TASKS
# -----------------------------

$Tasks = @()

foreach ($Line in Get-Content $TasksFile) {

    if ($Line -match '^- \[(?<state>[ Xx])\]\s+(?<id>T\d{3})\s*(?<text>.*)$') {

        $TaskId = $Matches["id"]
        $TaskState = $Matches["state"]
        $TaskText = $Matches["text"]

        $Tasks += [PSCustomObject]@{
            Id        = $TaskId
            Number    = [int]$TaskId.Substring(1)
            Completed = ($TaskState -eq "X" -or $TaskState -eq "x")
            Text      = $TaskText.Trim()
        }
    }
}

$CompletedTasks = @(
    $Tasks |
    Where-Object Completed |
    Sort-Object Number
)

$PendingTasks = @(
    $Tasks |
    Where-Object { -not $_.Completed } |
    Sort-Object Number
)

$Total = $Tasks.Count
$Completed = $CompletedTasks.Count
$Pending = $PendingTasks.Count

if ($Total -gt 0) {
    $Percent = [math]::Round(($Completed / $Total) * 100, 1)
}
else {
    $Percent = 0
}

$Last = $CompletedTasks |
    Select-Object -Last 1

$Next = $PendingTasks |
    Select-Object -First 1

$NextSix = @(
    $PendingTasks |
    Select-Object -First 6
)

if ($Last) {
    $LastText = "**$($Last.Id)** $($Last.Text)"
}
else {
    $LastText = "Ninguna"
}

if ($Next) {
    $NextText = "**$($Next.Id)** $($Next.Text)"
}
else {
    $NextText = "✅ Feature completada"
}

$NextMd = if ($NextSix.Count -gt 0) {
    (
        $NextSix |
        ForEach-Object {
            $Description = $_.Text

            if ($Description.Length -gt 150) {
                $Description = $Description.Substring(0,147) + "..."
            }

            "- [ ] **$($_.Id)** $Description"
        }
    ) -join "`r`n"
}
else {
    "✅ No quedan tareas."
}

$Now = Get-Date -Format "dd/MM/yyyy HH:mm:ss"

# -----------------------------
# DASHBOARD
# -----------------------------

$Dashboard = @"
# Dashboard automático — Kryon

> [!warning]
> Archivo generado automáticamente.
> No editar manualmente.

**Última actualización:** $Now

## Feature

``$Feature``

## Progreso

| Métrica | Valor |
|---|---:|
| Total | $Total |
| Completadas | $Completed |
| Pendientes | $Pending |
| Avance | $Percent % |

## Última tarea completada

$LastText

## Siguiente tarea

$NextText

## Próximas tareas

$NextMd

## Git

**Branch:** ``$Branch``

**HEAD:** ``$Hash`` — $Commit

### Cambios locales

$StatusMd

## Navegación

- [[00-Control-Center]]
- [[features/001-Gestion-Usuarios]]
- [[../../specs/$Feature/tasks|tasks.md]]
- [[../../specs/$Feature/spec|spec.md]]
- [[../../specs/$Feature/plan|plan.md]]

---

Generado por ``scripts/Update-KryonObsidian.ps1``.
"@

Set-Content $DashboardFile $Dashboard -Encoding UTF8

# -----------------------------
# CONTROL CENTER
# -----------------------------

$AutoSection = @"
<!-- KRYON-AUTO-START -->

## 🤖 Estado automático

**Última sincronización:** $Now

**Branch:** ``$Branch``

**HEAD:** ``$Hash`` — $Commit

**Progreso:** $Completed / $Total — **$Percent %**

**Última completada:** $LastText

**Siguiente:** $NextText

➡️ [[05-Dashboard-Automatico|Abrir dashboard automático]]

<!-- KRYON-AUTO-END -->
"@

$Control = Get-Content $ControlFile -Raw

$Pattern = '(?s)<!-- KRYON-AUTO-START -->.*?<!-- KRYON-AUTO-END -->'

if ($Control -match $Pattern) {
    $Control = [regex]::Replace($Control, $Pattern, $AutoSection)
}
else {
    $Control = $Control.Replace(
        "# Kryon — Control Center",
        "# Kryon — Control Center`r`n`r`n$AutoSection"
    )
}

Set-Content $ControlFile $Control -Encoding UTF8

Write-Host ""
Write-Host "Kryon + Obsidian sincronizados."
Write-Host "Feature:   $Feature"
Write-Host "Branch:    $Branch"
Write-Host "HEAD:      $Hash"
Write-Host "Progreso:  $Completed/$Total ($Percent%)"

if ($Next) {
    Write-Host "Siguiente: $($Next.Id)"
}

Write-Host ""
