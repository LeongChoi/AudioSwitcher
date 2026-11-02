param([switch]$Run)
$ErrorActionPreference = 'Stop'
try {
    if ($env:OS -ne 'Windows_NT') { throw 'Build this app on Windows 10 or Windows 11.' }
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) {
        $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
    }
    if (-not (Test-Path -LiteralPath $compiler)) {
        throw 'The .NET Framework C# compiler was not found. Enable/install .NET Framework 4.8 and try again.'
    }
    $output = Join-Path $PSScriptRoot 'AudioSwitcherModern.exe'
    if (Get-Process -Name 'AudioSwitcherModern' -ErrorAction SilentlyContinue) {
        throw 'Exit Audio Switcher Modern from its tray menu before rebuilding.'
    }
    $compilerArguments = @(
        '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/warn:4', '/codepage:65001',
        "/out:$output", "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')",
        '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll', '/reference:System.Xml.dll',
        (Join-Path $PSScriptRoot 'CoreAudio.cs'), (Join-Path $PSScriptRoot 'Program.cs')
    )
    Write-Host 'Building Audio Switcher Modern...' -ForegroundColor Cyan
    & $compiler @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed (exit code $LASTEXITCODE). Copy the error text for troubleshooting." }
    if (-not (Test-Path -LiteralPath $output)) { throw 'The compiler did not create the executable.' }
    Write-Host "Built successfully: $output" -ForegroundColor Green
    Write-Host 'You can run AudioSwitcherModern.exe now. Keep it in a permanent folder before enabling Start with Windows.'
    if ($Run) { Start-Process -FilePath $output }
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
