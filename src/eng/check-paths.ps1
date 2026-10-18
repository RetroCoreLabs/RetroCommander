# Fails when a tracked file holds a path that is only right on one machine.
#
# The repository is public. A path such as a drive-letter folder under a user's home, a Dev
# folder, a WSL /mnt/<drive>/ mount or a synced cloud-drive folder names one person's disk: it is wrong
# for everybody else and tells the world how that disk is laid out. Test data and demo images
# are found by walking up to src/demo instead (see the NDFS window tests).
#
# What is NOT caught, on purpose: made-up drive-letter inputs in tests such as
# "file:///C:/test/file.img" or C:\Users\me\Projects. They are the thing under test - the
# code has to handle Windows paths - and they name no real machine.
#
# Usage, from anywhere inside the repository:
#   pwsh src/eng/check-paths.ps1
# Exit code 0 = clean, 1 = machine paths found (each one is listed).
#
# The patterns are written so this file does not match itself.

$ErrorActionPreference = 'Stop'

$patterns = @(
    '[A-Za-z]:[\\/]+(Dev|Repos)[\\/]',     # a Dev or Repos folder on a drive
    '[\\/]Users[\\/]ronny([\\/]|$)',       # a real home folder (Windows or macOS)
    '/home/ronny([/]|$)',                  # a real home folder (Linux)
    '/mnt/[a-z]/',                         # a Windows drive seen from WSL
    'One[D]rive'                           # a synced cloud folder
)

$root = (& git rev-parse --show-toplevel).Trim()
$hits = @()
for ($i = 0; $i -lt $patterns.Count; $i++) {
    # -I skips binary files; -n gives line numbers. git grep exits 1 when nothing matches.
    $found = & git -C $root grep -n -I -E $patterns[$i]
    if ($LASTEXITCODE -eq 0) { $hits += $found }
}

if ($hits.Count -gt 0) {
    Write-Host "Machine-specific paths found. Use a path relative to the repository instead:"
    $hits | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "No machine-specific paths."
exit 0
