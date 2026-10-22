param(
    [string]$Feature = "001-gestion-usuarios"
)

$ErrorActionPreference = "Stop"

$Repo = Split-Path $PSScriptRoot -Parent
Set-Location $Repo

$TasksFile = Join-Path $Repo "specs\$Feature\tasks.md"
$DashboardFile = Join-Path $Repo "docs\kryon\05-Dashboard-Automatico.local.md"

if (-not (Test-Path $TasksFile)) {
    throw "No existe tasks.md para $Feature"
}

# Git
$Branch = (git branch --show-current).Trim()
$Hash = (git rev-parse --short HEAD).Trim()
$Commit = (git log -1 --pretty=%s).Trim()

$GitStatus = @(
    git status --short
)

if ($GitStatus.Count -eq 0) {
    $GitStatusText = "Working tree limpio."
}
else {
    $GitStatusText = ($GitStatus | ForEach-Object { "- ``$_``" }) -join "`r`n"
}

# Tasks
$Tasks = @()

$Utf8 = New-Object System.Text.UTF8Encoding($false)

$Lines = [System.IO.File]::ReadAllLines(
    $TasksFile,
    [System.Text.Encoding]::UTF8
)

foreach ($Line in $Lines) {

    if ($Line -match '^- \[(?<state>[ Xx])\]\s+(?<id>T\d{3})\s*(?<text>.*)$') {

        $TaskId = $Matches["id"]
        $TaskState = $Matches["state"]
        $TaskText = $Matches["text"]

        $Tasks += [PSCustomObject]@{
            Id = $TaskId
            Number = [int]$TaskId.Substring(1)
            Completed = ($TaskState -eq "X" -or $TaskState -eq "x")
            Text = $TaskText.Trim()
        }
    }
}

$CompletedTasks = @(
    $Tasks |
    Where-Object { $_.Completed } |
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

$LastTask = $CompletedTasks | Select-Object -Last 1
$NextTask = $PendingTasks | Select-Object -First 1
$NextSix = @($PendingTasks | Select-Object -First 6)

if ($LastTask) {
    $LastText = "**$($LastTask.Id)** $($LastTask.Text)"
}
else {
    $LastText = "Ninguna"
}

if ($NextTask) {
    $NextText = "**$($NextTask.Id)** $($NextTask.Text)"
}
else {
    $NextText = "Feature completada"
}

if ($NextSix.Count -gt 0) {

    $NextLines = @()

    foreach ($Task in $NextSix) {

        $Description = $Task.Text

        if ($Description.Length -gt 150) {
            $Description = $Description.Substring(0,147) + "..."
        }

        $NextLines += "- [ ] **$($Task.Id)** $Description"
    }

    $NextTasksText = $NextLines -join "`r`n"
}
else {
    $NextTasksText = "No quedan tareas pendientes."
}

$Now = Get-Date -Format "dd/MM/yyyy HH:mm:ss"

$Dashboard = @"
# Dashboard automatico - Kryon

> Archivo generado automaticamente.
> No editar manualmente.

**Ultima actualizacion:** $Now

## Feature activa

``$Feature``

## Progreso

| Metrica | Valor |
|---|---:|
| Total | $Total |
| Completadas | $Completed |
| Pendientes | $Pending |
| Avance | $Percent % |

## Ultima tarea completada

$LastText

## Siguiente tarea

$NextText

## Proximas tareas

$NextTasksText

## Git

**Branch:** ``$Branch``

**HEAD:** ``$Hash`` - $Commit

### Cambios locales

$GitStatusText

## Navegacion

- [[00-Control-Center]]
- [[features/001-Gestion-Usuarios]]
- [[../../specs/$Feature/tasks|tasks.md]]
- [[../../specs/$Feature/spec|spec.md]]
- [[../../specs/$Feature/plan|plan.md]]

---

Generado por ``scripts/Update-KryonObsidian.ps1``.
"@

[System.IO.File]::WriteAllText(
    $DashboardFile,
    $Dashboard,
    $Utf8
)

Write-Host ""
Write-Host "Kryon + Obsidian sincronizados."
Write-Host "Feature:   $Feature"
Write-Host "Branch:    $Branch"
Write-Host "HEAD:      $Hash"
Write-Host "Progreso:  $Completed/$Total ($Percent%)"

if ($NextTask) {
    Write-Host "Siguiente: $($NextTask.Id)"
}

Write-Host ""
