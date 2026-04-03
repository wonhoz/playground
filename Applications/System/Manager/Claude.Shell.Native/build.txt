$workDir = "C:\Users\admin\source\repos\+Playground\Applications\System\Manager\Claude.Shell.Native\bin\Release"
$sdk = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64"
& "$sdk\makeappx.exe" pack /d $workDir /p "C:\Users\admin\source\repos\+Playground\Applications\System\Manager\Claude.Shell.Native\bin\ClaudeContextMenu.msix" /nv /o
