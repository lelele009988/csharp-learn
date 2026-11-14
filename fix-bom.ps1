# ============================================================================
#  fix-bom.ps1  ---  convert every .cs file under this folder to UTF-8 WITH BOM
#
#  WHY THIS IS NEEDED
#    This machine's default ANSI code page is GBK (cp936). When the C# compiler
#    reads a .cs file that has NO byte-order mark, it falls back to that code
#    page, so Chinese comments and string literals become garbage -- and a
#    mis-decoded byte pair can even terminate a string literal early, which
#    breaks the build outright. Adding a UTF-8 BOM makes the compiler read the
#    file as UTF-8.
#
#  WHY THIS SCRIPT IS PURE ASCII  (do not "fix" this)
#    Ironically, a script whose job is to fix encoding problems must not have an
#    encoding problem itself. Windows PowerShell 5.1 also reads .ps1 files using
#    the ANSI code page when there is no BOM -- so Chinese text in here would
#    break this script exactly the way it breaks .cs files.
#    The first version of this file made that very mistake and failed to parse.
#    If you ever want Chinese output here, save THIS file as UTF-8 with BOM too.
#
#  USAGE (from the csharp-learn folder)
#      powershell -ExecutionPolicy Bypass -File .\fix-bom.ps1
# ============================================================================

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }

Write-Output "Scanning: $root"
Write-Output ""

$files = Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue |
         Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' }
if (-not $files) {
    Write-Output "No .cs file found."
    exit 0
}

$bom   = [byte[]](0xEF, 0xBB, 0xBF)
$added = 0
$kept  = 0

foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)

    $hasBom = ($bytes.Length -ge 3 -and
               $bytes[0] -eq 0xEF -and
               $bytes[1] -eq 0xBB -and
               $bytes[2] -eq 0xBF)

    if ($hasBom) {
        $kept++
        continue
    }

    $new = New-Object byte[] ($bom.Length + $bytes.Length)
    [Array]::Copy($bom,   0, $new, 0,          $bom.Length)
    [Array]::Copy($bytes, 0, $new, $bom.Length, $bytes.Length)
    [System.IO.File]::WriteAllBytes($f.FullName, $new)

    Write-Output "  [BOM added] $($f.FullName)"
    $added++
}

Write-Output ""
Write-Output "Done. added=$added  already-had-bom=$kept"
