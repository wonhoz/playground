@echo off
echo Building PhotoVideoOrganizer...
dotnet publish -c Release
echo.
echo Done! Output: bin\Release\net8.0-windows\win-x64\publish\PhotoVideoOrganizer.exe
pause
