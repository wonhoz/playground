$dir = 'C:\Users\admin\source\repos\+Playground\Applications\Files\Deep.Diff'
Get-ChildItem -Path $dir -Recurse -Filter '*.xaml' | ForEach-Object {
    $content = [System.IO.File]::ReadAllText($_.FullName)
    $fixed = $content -replace ' Spacing="[0-9]+"', ''
    [System.IO.File]::WriteAllText($_.FullName, $fixed)
    Write-Host "Fixed: $($_.Name)"
}
