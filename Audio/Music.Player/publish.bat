@echo off
echo Building MusicPlayer...
dotnet publish -c Release
echo.
echo Done! Output: bin\Release\net8.0-windows\win-x64\publish\MusicPlayer.exe
pause
