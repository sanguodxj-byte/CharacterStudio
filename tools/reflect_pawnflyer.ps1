
$managed = 'D:\steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed'
[void][Reflection.Assembly]::LoadFrom((Join-Path $managed 'UnityEngine.CoreModule.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $managed 'UnityEngine.IMGUIModule.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
$t = [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetType('Verse.PawnFlyer', $false) } | Where-Object { $_ } | Select-Object -First 1
Write-Host "TYPE $($t.FullName)"
$t.GetMethods([Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly') |
  Where-Object { $_.Name -match 'RespawnPawn|Tick|Destroy|ExposeData' } |
  Sort-Object Name |
  Select-Object Name, IsVirtual, @{Name='Params';Expression={($_.GetParameters() | ForEach-Object {$_.ParameterType.Name + ' ' + $_.Name}) -join ', '}}, @{Name='ReturnType';Expression={$_.ReturnType.Name}} |
  Format-Table -Wrap -AutoSize
Write-Host 'PROPS:'
$t.GetProperties([Reflection.BindingFlags]'Public,NonPublic,Instance') |
  Where-Object { $_.Name -match 'FlyingPawn|DestinationPos|ticksFlightTime' } |
  Select-Object Name, PropertyType, CanRead, CanWrite |
  Format-Table -AutoSize
