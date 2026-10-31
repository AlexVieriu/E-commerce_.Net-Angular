<#
.SYNOPSIS
  Shows two security bugs and their fixes, one step at a time.
  Each step starts the shop's server as it was BEFORE or AFTER a fix; Postman then runs the matching folder.

.EXAMPLE
  .\demo.ps1          # walk through the 4 steps (press Enter between them)
  .\demo.ps1 -Clean   # remove the demo copy of the code when you are done
#>
param([switch]$Clean)

$ErrorActionPreference = 'Stop'

$repo = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
$demoCopy = Join-Path (Split-Path $repo -Parent) 'skinet-demo'
$port = 7096

# The 4 steps: which version of the code to start, which Postman folder to run, what to expect.
$steps = @(
    @{ Code = 'bb6f853';     Folder = '1 · BUG-18 IDOR · pre-fix'
       Show = 'BEFORE the fix: user B can read and change user A''s cart.'; Expect = 'RED (2 failed)' }
    @{ Code = 'development'; Folder = '2 · BUG-18 IDOR · fixed'
       Show = 'AFTER the fix: user B only ever sees their own cart.';       Expect = 'GREEN (all passed)' }
    @{ Code = '487a41b';     Folder = '3 · BUG-19 coupon · pre-fix'
       Show = 'BEFORE the fix: a faked $999 coupon is accepted.';           Expect = 'RED (discount 999)' }
    @{ Code = 'development'; Folder = '4 · BUG-19 coupon · fixed'
       Show = 'AFTER the fix: only the real $5 coupon counts.';             Expect = 'GREEN (all passed)' }
)

function Stop-Server {
    Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
}

function Start-Server($code) {
    Stop-Server

    # A separate copy of the code, switched to the version this step needs.
    if (Test-Path $demoCopy) { git -C $demoCopy checkout --quiet --force --detach $code }
    else { git -C $repo worktree add --quiet --detach $demoCopy $code }

    # Start from an empty shop every time: fresh database, no leftover cart.
    Remove-Item "$demoCopy\skinet\data\store.db*" -Force -ErrorAction SilentlyContinue
    docker compose -f "$repo\skinet\docker-compose.yml" up -d redis 2>&1 | Out-Null
    docker exec redis redis-cli DEL cart-of-A | Out-Null

    $api = "$demoCopy\skinet\src\API"
    dotnet build "$api\API.csproj" -v q -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "The server did not build. Run: dotnet build `"$api\API.csproj`"" }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    Start-Process dotnet -ArgumentList 'run', '--no-build', '--no-launch-profile', '--urls', "https://localhost:$port" `
        -WorkingDirectory $api -WindowStyle Hidden

    # Wait until the server answers (up to 90 seconds).
    foreach ($second in 1..90) {
        if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { return }
        Start-Sleep -Seconds 1
    }
    throw 'The server did not start within 90 seconds.'
}

if ($Clean) {
    Stop-Server
    if (Test-Path $demoCopy) { git -C $repo worktree remove --force $demoCopy }
    git -C $repo worktree prune
    Write-Host 'Demo copy removed.'
    return
}

try {
    for ($i = 0; $i -lt $steps.Count; $i++) {
        $step = $steps[$i]
        Write-Host ''
        Write-Host "Step $($i + 1) of 4 — starting the server..." -ForegroundColor Cyan
        Start-Server $step.Code

        Write-Host $step.Show -ForegroundColor White
        Write-Host "In Postman: folder '$($step.Folder)' -> Run folder -> Start run." -ForegroundColor Yellow
        Write-Host "Expected: $($step.Expect)" -ForegroundColor $(if ($step.Expect -like 'RED*') { 'Red' } else { 'Green' })
        if ($i -lt $steps.Count - 1) { Read-Host 'Press Enter for the next step' | Out-Null }
        else { Read-Host 'Press Enter to finish' | Out-Null }
    }
}
finally {
    Stop-Server
    Write-Host 'Server stopped. Demo finished.' -ForegroundColor Cyan
}
