$src = @'
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

public static class ILInspector
{
    private static readonly Dictionary<ushort, OpCode> OpCodeMap = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode))
        .Select(f => (OpCode)f.GetValue(null))
        .ToDictionary(o => unchecked((ushort)o.Value));

    public static void PrintCalls(string asmPath, string typeName, string methodName)
    {
        Assembly asm = Assembly.LoadFrom(asmPath);
        Type type = asm.GetType(typeName);
        if (type == null)
        {
            Console.WriteLine("TYPE_NOT_FOUND " + typeName);
            return;
        }

        MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == methodName)
            .ToArray();
        if (methods.Length == 0)
        {
            Console.WriteLine("METHOD_NOT_FOUND " + typeName + "." + methodName);
            return;
        }

        foreach (MethodInfo method in methods)
        {
            Console.WriteLine("=== " + typeName + "." + method.Name + " ===");
            MethodBody body = method.GetMethodBody();
            if (body == null)
            {
                Console.WriteLine("NO_BODY");
                continue;
            }

            byte[] il = body.GetILAsByteArray();
            int i = 0;
            while (i < il.Length)
            {
                ushort code = il[i++];
                if (code == 0xFE)
                {
                    code = (ushort)(0xFE00 | il[i++]);
                }

                OpCode op;
                if (!OpCodeMap.TryGetValue(code, out op))
                {
                    Console.WriteLine("UNKNOWN_OPCODE 0x" + code.ToString("X"));
                    break;
                }

                switch (op.OperandType)
                {
                    case OperandType.InlineMethod:
                        int token = BitConverter.ToInt32(il, i);
                        i += 4;
                        try
                        {
                            MethodBase target = method.Module.ResolveMethod(token);
                            Console.WriteLine(op.Name + " " + target.DeclaringType.FullName + "." + target.Name);
                        }
                        catch
                        {
                            Console.WriteLine(op.Name + " token:" + token);
                        }
                        break;
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        i += 1;
                        break;
                    case OperandType.InlineVar:
                        i += 2;
                        break;
                    case OperandType.InlineI:
                    case OperandType.InlineBrTarget:
                    case OperandType.InlineField:
                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                    case OperandType.InlineSig:
                    case OperandType.InlineString:
                        i += 4;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        i += 8;
                        break;
                    case OperandType.ShortInlineR:
                        i += 4;
                        break;
                    case OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(il, i);
                        i += 4 + (count * 4);
                        break;
                    default:
                        Console.WriteLine("UNHANDLED_OPERAND " + op.OperandType);
                        return;
                }
            }
        }
    }
}
'@

Add-Type -TypeDefinition $src

$asmPath = 'D:\steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll'

[ILInspector]::PrintCalls($asmPath, 'Verse.Pawn', 'Tick')
[ILInspector]::PrintCalls($asmPath, 'Verse.Thing', 'DoTick')
[ILInspector]::PrintCalls($asmPath, 'Verse.Map', 'MapPreTick')
[ILInspector]::PrintCalls($asmPath, 'Verse.Map', 'MapPostTick')
[ILInspector]::PrintCalls($asmPath, 'Verse.Map', 'MapUpdate')
[ILInspector]::PrintCalls($asmPath, 'Verse.MapDrawer', 'MapMeshDrawerUpdate_First')
[ILInspector]::PrintCalls($asmPath, 'RimWorld.Planet.GlobalRendererUtility', 'UpdateGlobalShadersParams')
[ILInspector]::PrintCalls($asmPath, 'Verse.WaterInfo', 'SetTextures')
[ILInspector]::PrintCalls($asmPath, 'Verse.SectionLayer_Watergen', 'DrawLayer')
[ILInspector]::PrintCalls($asmPath, 'Verse.SectionLayer_Watergen', 'GetMaterialFor')
[ILInspector]::PrintCalls($asmPath, 'Verse.Game', 'FillComponents')

$asm = [Reflection.Assembly]::LoadFrom($asmPath)
Write-Output '=== WATER_TYPES ==='
try
{
    $asm.GetTypes() | Where-Object { $_.FullName -like '*Water*' -or $_.Name -like '*Water*' } | Sort-Object FullName | ForEach-Object { $_.FullName }
}
catch [Reflection.ReflectionTypeLoadException]
{
    $_.Exception.Types | Where-Object { $_ -ne $null -and ($_.FullName -like '*Water*' -or $_.Name -like '*Water*') } | Sort-Object FullName | ForEach-Object { $_.FullName }
}

$cameraFind = $asm.GetType('Verse.Find')
$cameraProp = $cameraFind.GetProperty('CameraColor', [Reflection.BindingFlags]'Static,Public,NonPublic')
if ($cameraProp -ne $null)
{
    Write-Output ('=== CAMERA_COLOR_PROP === ' + $cameraProp.PropertyType.FullName)
}
