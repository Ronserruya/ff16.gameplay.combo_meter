# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/ff16.gameplay.combo_meter/*" -Force -Recurse
dotnet publish "./ff16.gameplay.combo_meter.csproj" -c Release -o "$env:RELOADEDIIMODS/ff16.gameplay.combo_meter" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location