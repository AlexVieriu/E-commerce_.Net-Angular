# Launch-Chrome-Debug.ps1
# ----------------------------------------------------------------------------
# What: launches Chrome for Testing with remote debugging (port 9222) + an
#       isolated profile, then opens the Angular dev app (https://localhost:4200).
# Why:  lets VS Code attach for breakpoint debugging of the Angular client
#       (the "Angular Debug (Manual Chrome)" launch config). --disable-web-security
#       drops CORS for frictionless local debugging — DEV ONLY, never for browsing.
# When: only needed if you debug Angular via VS Code attach. For plain browser
#       DevTools debugging you don't need this. Requires Chrome for Testing at
#       the hardcoded $ChromePath below (update it if your install moves).
# ----------------------------------------------------------------------------
# PowerShell script to launch Chrome for Testing with debugging enabled
Write-Host "Starting Chrome for Testing with debugging enabled for VS Code..." -ForegroundColor Green

# Get the current script directory (project root)
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$DebugProfilePath = Join-Path $ProjectRoot ".vscode\chrome-debug-profile"

# Create debug profile directory if it doesn't exist
if (-not (Test-Path $DebugProfilePath)) {
    New-Item -ItemType Directory -Path $DebugProfilePath -Force
    Write-Host "Created debug profile directory: $DebugProfilePath" -ForegroundColor Yellow
}

# Chrome for Testing executable path
$ChromePath = "C:\Users\Alex\Downloads\chrome-win64\chrome.exe"

# Verify Chrome executable exists
if (-not (Test-Path $ChromePath)) {
    Write-Host "ERROR: Chrome executable not found at: $ChromePath" -ForegroundColor Red
    Write-Host "Please verify the Chrome for Testing installation path." -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# Launch Chrome for Testing with debugging flags
$ChromeArgs = @(
    "--remote-debugging-port=9222",
    "--user-data-dir=`"$DebugProfilePath`"",
    "--disable-web-security",
    "--disable-features=VizDisplayCompositor",
    "--no-first-run",
    "--no-default-browser-check",
    "https://localhost:4200/"
)

Write-Host "Launching Chrome for Testing with debugging enabled..." -ForegroundColor Green
Write-Host "Debug profile: $DebugProfilePath" -ForegroundColor Cyan
Write-Host "Debugging port: 9222" -ForegroundColor Cyan

try {
    Start-Process -FilePath $ChromePath -ArgumentList $ChromeArgs
    Write-Host "Chrome for Testing launched successfully!" -ForegroundColor Green
    Write-Host "You can now start 'Angular Debug (Manual Chrome)' in VS Code" -ForegroundColor Yellow
} catch {
    Write-Host "ERROR: Failed to launch Chrome: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Press Enter to close this window..." -ForegroundColor Gray
Read-Host