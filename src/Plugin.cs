using BepInEx;
using MonoMod.Cil;
using RWCustom;
using System;
using UnityEngine;

namespace MovementTweaks;

[BepInPlugin("zombieseatflesh7.MovementTweaks", "Movement Tweaks", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    public static new BepInEx.Logging.ManualLogSource Logger;

    public void OnEnable()
    {
        Logger = base.Logger;

        On.RainWorld.OnModsInit += OnModsInit;
        Hooks.InitHooks();
    }

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        MachineConnector.SetRegisteredOI("MovementTweaks", Options.instance);
    }

    // copied from simplified moveset. 
    public static void LogAllInstructions(ILContext? context, int index_string_length = 9, int op_code_string_length = 14)
    {
        if (context == null) return;

        Logger.LogInfo("-----------------------------------------------------------------");
        Logger.LogInfo("Log all IL-instructions.");
        Logger.LogInfo("Index:" + new string(' ', index_string_length - 6) + "OpCode:" + new string(' ', op_code_string_length - 7) + "Operand:");

        ILCursor cursor = new(context);
        ILCursor label_cursor = cursor.Clone();

        string cursor_index_string;
        string op_code_string;
        string operand_string;

        while (true)
        {
            // this might return too early;
            // if (cursor.Next.MatchRet()) break;

            // should always break at some point;
            // only TryGotoNext() doesn't seem to be enough;
            // it still throws an exception;
            try
            {
                if (cursor.TryGotoNext(MoveType.Before))
                {
                    cursor_index_string = cursor.Index.ToString();
                    cursor_index_string = cursor_index_string.Length < index_string_length ? cursor_index_string + new string(' ', index_string_length - cursor_index_string.Length) : cursor_index_string;
                    op_code_string = cursor.Next.OpCode.ToString();

                    if (cursor.Next.Operand is ILLabel label)
                    {
                        label_cursor.GotoLabel(label);
                        operand_string = "Label >>> " + label_cursor.Index;
                    }
                    else
                    {
                        operand_string = cursor.Next.Operand?.ToString() ?? "";
                    }

                    if (operand_string == "")
                    {
                        Logger.LogInfo(cursor_index_string + op_code_string);
                    }
                    else
                    {
                        op_code_string = op_code_string.Length < op_code_string_length ? op_code_string + new string(' ', op_code_string_length - op_code_string.Length) : op_code_string;
                        Logger.LogInfo(cursor_index_string + op_code_string + operand_string);
                    }
                }
                else
                {
                    break;
                }
            }
            catch
            {
                break;
            }
        }
        Logger.LogInfo("-----------------------------------------------------------------");
    }
}
