# Phase 0 : cree/ouvre le projet Unity Game/, configure URP, construit la scene, build Windows et mesure.
# Usage (PowerShell, depuis la racine du depot) :  .\Tools\Unity\phase0.ps1
# Necessite une licence Unity active (Unity Hub > Preferences > Licences).
param(
    [string]$Unity = "",
    [int]$Seconds = 20,
    [string]$OutDir = ""
)
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$Game = Join-Path $Root "Game"
$Logs = Join-Path $Root "Builds\Logs"
New-Item -ItemType Directory -Force $Logs | Out-Null

if (-not $Unity) {
    $candidates = @(Get-ChildItem "C:\Program Files\Unity\Hub\Editor\6000.*\Editor\Unity.exe", "C:\Program Files\Unity 6000.*\Editor\Unity.exe" -ErrorAction SilentlyContinue)
    $Unity = ($candidates | Sort-Object { [version](($_.FullName -replace '.*6000\.(\d+)\.(\d+).*', '6000.$1.$2')) } | Select-Object -Last 1).FullName
}
if (-not $Unity) { throw "Aucun editeur Unity 6000.x trouve" }
Write-Host "Unity : $Unity"

function Invoke-Unity([string]$Name, [string[]]$UnityArgs) {
    $log = Join-Path $Logs "$Name.log"
    $p = Start-Process $Unity -ArgumentList ($UnityArgs + @("-logFile", "`"$log`"")) -Wait -PassThru -NoNewWindow
    $errors = Select-String -Path $log -Pattern "error CS\d+|Compilation failed|Scripts have compiler errors" -ErrorAction SilentlyContinue
    if ($p.ExitCode -ne 0 -or $errors) {
        $errors | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line }
        Get-Content $log -Tail 30
        throw "Etape '$Name' en echec (code $($p.ExitCode)), voir $log"
    }
    Write-Host "OK : $Name"
}

# 1. Creation du projet (une seule fois) ; LISEZMOI.md est mis de cote pendant la creation
if (-not (Test-Path (Join-Path $Game "ProjectSettings\ProjectVersion.txt"))) {
    $readme = Join-Path $Game "LISEZMOI.md"
    $tmp = Join-Path $env:TEMP "aftercity_LISEZMOI.md"
    if (Test-Path $readme) { Move-Item $readme $tmp -Force }
    try { Invoke-Unity "01_create" @("-batchmode", "-quit", "-createProject", "`"$Game`"") }
    finally { if (Test-Path $tmp) { Move-Item $tmp $readme -Force } }
}

# 2. Paquets : moteur After City + URP (version fournie par l'editeur)
$manifestPath = Join-Path $Game "Packages\manifest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$deps = $manifest.dependencies
if (-not $deps.'com.aftercity.core') { $deps | Add-Member -NotePropertyName "com.aftercity.core" -NotePropertyValue "file:../../Core" }
if (-not $deps.'com.unity.render-pipelines.universal') {
    $urp = Get-Content (Join-Path (Split-Path $Unity) "Data\Resources\PackageManager\BuiltInPackages\com.unity.render-pipelines.universal\package.json") -Raw | ConvertFrom-Json
    $deps | Add-Member -NotePropertyName "com.unity.render-pipelines.universal" -NotePropertyValue $urp.version
}
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 10))

# 3. Configuration URP, scene, build
Invoke-Unity "02_setup" @("-batchmode", "-quit", "-projectPath", "`"$Game`"", "-executeMethod", "AfterCity.EditorTools.Phase0Builder.SetupProject")
Invoke-Unity "03_scene" @("-batchmode", "-quit", "-projectPath", "`"$Game`"", "-executeMethod", "AfterCity.EditorTools.Phase0Builder.BuildScene")
$outArgs = @()
if ($OutDir) { $outArgs = @("-aftercity-out", "`"$OutDir`"") }
Invoke-Unity "04_build" (@("-batchmode", "-quit", "-projectPath", "`"$Game`"", "-executeMethod", "AfterCity.EditorTools.Phase0Builder.BuildPlayer") + $outArgs)

# 4. Lancement mesure : ~20 s, capture d'ecran et FPS moyen
$out = if ($OutDir) { $OutDir } else { Join-Path $Root "Builds\Phase0" }
$exe = Join-Path $out "AfterCity.exe"
Remove-Item (Join-Path $out "fps.txt"), (Join-Path $out "screenshot.png") -ErrorAction SilentlyContinue
$p = Start-Process $exe -ArgumentList "-screen-fullscreen", "0", "-screen-width", "1920", "-screen-height", "1080", "-logFile", "`"$(Join-Path $Logs 'player.log')`"", "-aftercity-benchmark", "`"$out`"" -PassThru
if (-not $p.WaitForExit(($Seconds + 60) * 1000)) { $p.Kill(); throw "Le jeu ne s'est pas arrete tout seul" }
Get-Content (Join-Path $out "fps.txt")
Write-Host "Capture : $(Join-Path $out 'screenshot.png')"
