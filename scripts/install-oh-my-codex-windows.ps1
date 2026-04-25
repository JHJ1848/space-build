param(
    [switch]$InstallPsmux
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$installPsmuxRequested = $PSBoundParameters.ContainsKey("InstallPsmux") -and $InstallPsmux.IsPresent

function Require-Command {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Missing required command: $Name"
    }

    return $command
}

function New-BackupFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,
        [Parameter(Mandatory = $true)]
        [string]$BackupRoot
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        return
    }

    New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null
    $leafName = Split-Path -Leaf $SourcePath
    $targetPath = Join-Path $BackupRoot $leafName
    Copy-Item -LiteralPath $SourcePath -Destination $targetPath -Force
}

function Ensure-NodeVersion {
    $nodeVersionText = (& node --version).Trim()
    if (-not $nodeVersionText) {
        throw "Unable to read node version."
    }

    $normalized = $nodeVersionText.TrimStart("v")
    $major = [int]($normalized.Split(".")[0])
    if ($major -lt 20) {
        throw "Node.js 20+ is required. Current version: $nodeVersionText"
    }

    return $nodeVersionText
}

function Assert-PowerShell7 {
    if (-not $PSVersionTable.PSVersion) {
        throw "Unable to determine PowerShell version."
    }

    if ($PSVersionTable.PSVersion.Major -lt 7) {
        throw "Run this script in PowerShell 7+. Current version: $($PSVersionTable.PSVersion)"
    }
}

function Invoke-CmdChecked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [Parameter(Mandatory = $false)]
        [string]$WorkingDirectory
    )

    if ($WorkingDirectory) {
        Push-Location $WorkingDirectory
    }

    try {
        & cmd.exe /d /c $Command
        if ($LASTEXITCODE -ne 0) {
            throw ("Command failed with exit code {0}: {1}" -f $LASTEXITCODE, $Command)
        }
    }
    finally {
        if ($WorkingDirectory) {
            Pop-Location
        }
    }
}

function Clone-OhMyCodexRepo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    $repoUrl = "https://github.com/Yeachan-Heo/oh-my-codex.git"

    for ($attempt = 1; $attempt -le 3; $attempt++) {
        if (Test-Path $RepoRoot) {
            Remove-Item -LiteralPath $RepoRoot -Recurse -Force
        }

        try {
            Invoke-CmdChecked -Command "git clone --depth 1 --single-branch $repoUrl `"$RepoRoot`""
            return
        }
        catch {
            if ($attempt -eq 3) {
                throw
            }

            Write-Host "Git clone failed on attempt $attempt. Retrying..."
            Start-Sleep -Seconds (5 * $attempt)
        }
    }
}

function Install-PsmuxIfRequested {
    param(
        [switch]$Requested
    )

    if (-not $Requested) {
        Write-Host "Skipping psmux installation. Use -InstallPsmux for native Windows team mode."
        return
    }

    if (Get-Command psmux -ErrorAction SilentlyContinue) {
        Write-Host "psmux is already installed."
        return
    }

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw "winget is required to install psmux automatically."
    }

    Write-Host "Installing psmux via winget..."
    & winget install --id psmux.psmux --exact --accept-package-agreements --accept-source-agreements
}

Assert-PowerShell7
Require-Command -Name node | Out-Null
Require-Command -Name cmd.exe | Out-Null
Require-Command -Name git | Out-Null

$nodeVersion = Ensure-NodeVersion
Write-Host "Node.js version: $nodeVersion"
Write-Host "PowerShell version: $($PSVersionTable.PSVersion)"

$codexHome = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $HOME ".codex" }
New-Item -ItemType Directory -Force -Path $codexHome | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path $codexHome "backups\oh-my-codex-install-$timestamp"
New-BackupFile -SourcePath (Join-Path $codexHome "config.toml") -BackupRoot $backupRoot
New-BackupFile -SourcePath (Join-Path $codexHome "hooks.json") -BackupRoot $backupRoot

Install-PsmuxIfRequested -Requested:$installPsmuxRequested

$toolsRoot = Join-Path $codexHome "tools"
$repoRoot = Join-Path $toolsRoot "oh-my-codex"
New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null

Write-Host "Installing @openai/codex globally..."
Invoke-CmdChecked -Command "npm.cmd install -g @openai/codex"

if (Test-Path (Join-Path $repoRoot ".git")) {
    Write-Host "Refreshing oh-my-codex GitHub checkout..."
    Invoke-CmdChecked -Command "git fetch --all --tags --prune" -WorkingDirectory $repoRoot
    Invoke-CmdChecked -Command "git pull --ff-only" -WorkingDirectory $repoRoot
}
else {
    Write-Host "Cloning oh-my-codex from GitHub..."
    Clone-OhMyCodexRepo -RepoRoot $repoRoot
}

Write-Host "Installing oh-my-codex dependencies from GitHub checkout..."
Invoke-CmdChecked -Command "npm.cmd install" -WorkingDirectory $repoRoot

Write-Host "Building oh-my-codex..."
Invoke-CmdChecked -Command "npm.cmd run build" -WorkingDirectory $repoRoot

Write-Host "Removing any previous global oh-my-codex package..."
& cmd.exe /d /c "npm.cmd uninstall -g oh-my-codex"

Write-Host "Installing oh-my-codex globally from local GitHub checkout..."
Invoke-CmdChecked -Command "npm.cmd install -g `"$repoRoot`""

$omxCmd = Join-Path $env:APPDATA "npm\omx.cmd"
$omxPs1 = Join-Path $env:APPDATA "npm\omx.ps1"
if (-not (Test-Path $omxCmd) -and -not (Test-Path $omxPs1)) {
    throw "omx launcher was not created under $env:APPDATA\\npm"
}

Push-Location $HOME
try {
    Write-Host "Running user-scope setup..."
    if (Test-Path $omxCmd) {
        & $omxCmd setup --scope user
    }
    else {
        & $omxPs1 setup --scope user
    }

    if ($LASTEXITCODE -ne 0) {
        throw "omx setup failed with exit code $LASTEXITCODE"
    }

    Write-Host "Running doctor check..."
    if (Test-Path $omxCmd) {
        & $omxCmd doctor
    }
    else {
        & $omxPs1 doctor
    }

    if ($LASTEXITCODE -ne 0) {
        throw "omx doctor failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Installation completed."
Write-Host "Next recommended checks:"
Write-Host "  codex login status"
Write-Host '  omx exec --skip-git-repo-check -C . "Reply with exactly OMX-EXEC-OK"'
Write-Host "Optional native Windows team mode:"
Write-Host "  Re-run this script with -InstallPsmux"
