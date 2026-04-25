Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$logPath = "C:\Users\jinghongjie\Desktop\space-build\omx-install.log"
$statusPath = "C:\Users\jinghongjie\Desktop\space-build\omx-install.status"
$scriptPath = "C:\Users\jinghongjie\Desktop\space-build\scripts\install-oh-my-codex-windows.ps1"

Remove-Item $logPath, $statusPath -ErrorAction SilentlyContinue

try {
    & $scriptPath *>> $logPath
    "OK" | Out-File -FilePath $statusPath -Encoding utf8
    exit 0
}
catch {
    $_ | Out-String | Out-File -FilePath $logPath -Encoding utf8 -Append
    "FAIL" | Out-File -FilePath $statusPath -Encoding utf8
    exit 1
}
