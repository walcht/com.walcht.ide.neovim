$PPID = (Get-CimInstance Win32_Process -Filter "ProcessId = '$PID'").ParentProcessId
$pipe = New-Object System.IO.Pipes.NamedPipeServerStream("\\.\pipe\getprocessppidpipe")

Write-Host "create pipe server \\.\pipe\getprocessppidpipe"
Write-Host "waiting for client connection ..."

$pipe.WaitForConnection()

Write-Host "client connected"

$sw = New-Object System.IO.StreamWriter($pipe)
$sw.WriteLine($PPID)
$sw.Flush()

Write-Host "wrote $PPID"

$sw.Dispose()
$pipe.Dispose()

Write-Host "cleanup done"
