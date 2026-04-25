Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$out = "C:\Users\jinghongjie\Desktop\space-build\npm-probe.txt"
npm --version | Out-File -FilePath $out -Encoding utf8
