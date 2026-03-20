$pipe = New-Object System.IO.Pipes.NamedPipeClientStream("\\.\pipe\getprocessppidpipe")
$pipe.Connect(1000)

$sr = New-Object System.IO.StreamReader($pipe)
Write-Host $sr.ReadLine()

$sr.Dispose()
$pipe.Dispose()
