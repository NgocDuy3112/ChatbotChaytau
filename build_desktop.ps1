param(
    [string]$PythonExe = "$PSScriptRoot\\app\\.venv\\Scripts\\python.exe",
    [string]$OutputDir = "$PSScriptRoot",
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Command
    )

    & $Command[0] $Command[1..($Command.Length - 1)]
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $($Command -join ' ')"
    }
}

if (-not (Test-Path $PythonExe)) {
    throw "Python executable not found: $PythonExe"
}

$entryScript = Join-Path $PSScriptRoot "app\\client\\main.py"
if (-not (Test-Path $entryScript)) {
    throw "Entry script not found: $entryScript"
}

$workPath = Join-Path $PSScriptRoot "build\\pyinstaller\\work"
$specPath = Join-Path $PSScriptRoot "build\\pyinstaller\\spec"

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
New-Item -ItemType Directory -Path $workPath -Force | Out-Null
New-Item -ItemType Directory -Path $specPath -Force | Out-Null

if (-not $SkipInstall) {
    Invoke-CheckedCommand -Command @($PythonExe, "-m", "ensurepip", "--upgrade")
    Invoke-CheckedCommand -Command @($PythonExe, "-m", "pip", "install", "--upgrade", "pip", "pyinstaller")
}

Invoke-CheckedCommand -Command @(
    $PythonExe,
    "-m",
    "PyInstaller",
    "--noconfirm",
    "--clean",
    "--windowed",
    "--onefile",
    "--name",
    "ChatbotDesktop",
    "--distpath",
    $OutputDir,
    "--workpath",
    $workPath,
    "--specpath",
    $specPath,
    "--paths",
    $PSScriptRoot,
    "--add-data",
    "$PSScriptRoot/app/resources/instructions;resources/instructions",
    "--add-data",
    "$PSScriptRoot/app/resources/sheets;resources/sheets",
    "--add-data",
    "$PSScriptRoot/app/resources/icons;resources/icons",
    $entryScript
)

Write-Host "EXE created at: $OutputDir\\ChatbotDesktop.exe"
