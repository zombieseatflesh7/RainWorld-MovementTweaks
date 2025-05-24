using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MovementTweaks
{
    internal static class Hooks
    {
        internal static void InitHooks()
        {
            IL.Player.WallJump += WallJumpHook;
            //IL.Player.UpdateBodyMode += WallSlideHook;
            IL.Player.Update += UpdateHook;
        }

        private static void WallJumpHook(ILContext il)
        {
            ILCursor c = new(il);

            try
            {
                c.GotoNext(i => i.MatchCall("PhysicalObject", "IsTileSolid"));
                c.GotoNext(i => i.MatchCall("PhysicalObject", "IsTileSolid"));

                // REPLACE: if (IsTileSolid(1, 0, -1) || IsTileSolid(0, 0, -1) || base.bodyChunks[1].submersion > 0.1f || flag)
                // WITH:    if (IsTileSolidOrSlope(1, 0f, -12f) || IsTileSolidOrSlope(0, 0f, -12f) || base.bodyChunks[1].submersion > 0.1f || flag)
                c.GotoNext(i => i.MatchCall("PhysicalObject", "IsTileSolid"));
                c.Goto(c.Index - 2); c.RemoveRange(3); // second and third parameter, and function call
                c.Emit(OpCodes.Ldc_R4, 0f); c.Emit(OpCodes.Ldc_R4, -12f); // new second and third parameter
                c.EmitDelegate(IsTileSolidOrSlope);

                c.GotoNext(i => i.MatchCall("PhysicalObject", "IsTileSolid"));
                c.Goto(c.Index - 2); c.RemoveRange(3); // second and third parameter, and function call
                c.Emit(OpCodes.Ldc_R4, 0f); c.Emit(OpCodes.Ldc_R4, -12f); // new second and third parameter
                c.EmitDelegate(IsTileSolidOrSlope);

                // REPLACE: if (base.bodyChunks[1].ContactPoint.y > -1 && base.bodyChunks[0].ContactPoint.y > -1 && base.Submersion == 0f)
                // WITH:    if (!IsTileSolidOrSlope(1, 0f, -12f) && !IsTileSolidOrSlope(0, 0f, -12f) && base.Submersion == 0f)
                c.GotoNext(
                    i => i.MatchLdarg(0),
                    i => i.MatchCall(AccessTools.Method(typeof(PhysicalObject), "get_bodyChunks")),
                    i => i.MatchLdcI4(1),
                    i => i.MatchLdelemRef(),
                    i => i.MatchCallvirt(AccessTools.Method(typeof(BodyChunk), "get_ContactPoint"))
                    );
                int start = c.Index;
                c.GotoNext(i => i.MatchMul());
                int end = c.Index;
                c.Goto(start); c.RemoveRange(end - start);

                c.Emit(OpCodes.Ldloc_0); // num
                c.Emit(OpCodes.Ldarg_0); // this
                c.EmitDelegate<Func<Player, float>>(player =>
                {
                    if (!player.IsTileSolidOrSlope(1, 0f, -12f) && !player.IsTileSolidOrSlope(0, 0f, -12f) && player.Submersion == 0f)
                        return 0.7f;
                    return 1f;
                });
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError(e.Message + "\n" + e.StackTrace);
            }
        }

        // modified version of Player.IsTileSolid
        private static bool IsTileSolidOrSlope(this Player self, int bChunk, float relativeX, float relativeY)
        {
            IntVector2 pos = self.room.GetTilePosition(self.bodyChunks[bChunk].pos + new Vector2(relativeX, relativeY));
            switch (self.room.GetTile(pos).Terrain)
            {
                case Room.Tile.TerrainType.Solid:
                    return true;
                case Room.Tile.TerrainType.Floor:
                    if (relativeY < 0 && !self.bodyChunks[bChunk].goThroughFloors)
                    {
                        return true;
                    }
                    break;
                case Room.Tile.TerrainType.Slope:
                    return self.room.IdentifySlope(pos) != Room.SlopeDirection.Broken;
            }
            if (self.room.terrain != null)
            {
                Vector2 center = self.bodyChunks[bChunk].pos + new Vector2(relativeX, relativeY);
                float rad = self.bodyChunks[bChunk].rad;
                return self.room.terrain.SnapToTerrain(center, rad).y - center.y > rad;
            }
            return false;
        }

        private static void WallSlideHook(ILContext il)
        {
            ILCursor c = new(il);

            try
            {
                // base.bodyChunks[0].pos.y += base.gravity * Custom.LerpMap(wallSlideCounter, 0f, 30f, 0.8f, 0f) * EffectiveRoomGravity;
                // base.bodyChunks[1].pos.y += base.gravity * Custom.LerpMap(wallSlideCounter, 0f, 30f, 0.8f, 0f) * EffectiveRoomGravity;
                c.GotoNext(
                    i => i.MatchLdarg(0),
                    i => i.MatchCall("PhysicalObject", "get_bodyChunks"),
                    i => i.MatchLdcI4(0),
                    i => i.MatchLdelemRef(),
                    i => i.MatchLdflda("BodyChunk", "pos"),
                    i => i.MatchLdflda("UnityEngine.Vector2", "y"),
                    i => i.MatchDup(),
                    i => i.MatchLdindR4(),
                    i => i.MatchLdarg(0),
                    i => i.MatchCall("PhysicalObject", "get_gravity"),
                    i => i.MatchLdarg(0),
                    i => i.MatchLdfld("Player", "wallSlideCounter")
                    );
                int start = c.Index;
                c.GotoNext(i => i.MatchStindR4());
                c.GotoNext(i => i.MatchStindR4());
                int end = c.Index + 1;
                c.Goto(start);
                c.RemoveRange(end - start);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError(e.Message + "\n" + e.StackTrace);
            }
        }

        private static void UpdateHook(ILContext il)
        {
            ILCursor c = new(il);

            try
            {
                c.GotoNext(
                    i => i.MatchLdfld("Player", "bodyMode"),
                    i => i.MatchLdsfld("Player/BodyModeIndex", "Swimming"),
                    i => i.MatchCall("ExtEnum`1<Player/BodyModeIndex>", "op_Inequality")
                    );
                c.GotoNext(i => i.MatchLdarg(0));
                c.GotoNext();
                c.RemoveRange(5);
                c.EmitDelegate<Func<Player, bool>>(player => {
                    return player.bodyChunks[0].ContactPoint.x != 0 && (!Options.fastWallSlide.Value || player.input[0].y > -1);
                });

                c.GotoNext(i => i.MatchStindR4()); c.Goto(c.Index + 2);
                c.RemoveRange(5);
                c.EmitDelegate<Func<Player, bool>>(player => {
                    return player.bodyChunks[1].ContactPoint.x != 0 && (!Options.fastWallSlide.Value || player.input[0].y > -1);
                });
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError(e.Message + "\n" + e.StackTrace);
            }
        }
    }
}
