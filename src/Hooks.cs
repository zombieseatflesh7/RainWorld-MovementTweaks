using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
using UnityEngine;

namespace MovementTweaks
{
    internal static class Hooks
    {
        internal static void InitHooks()
        {
            IL.Player.Update += Player_UpdateIL;
            IL.Player.MovementUpdate += Player_MovementUpdateIL;
            IL.Player.WallJump += Player_WallJumpIL;
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

        // FAST WALL SLIDE
        private static void Player_UpdateIL(ILContext il)
        {
            ILCursor c = new(il);

            try
            {
                // if (bodyMode != BodyModeIndex.Swimming)
                c.GotoNext(
                    i => i.MatchLdfld("Player", "bodyMode"),
                    i => i.MatchLdsfld("Player/BodyModeIndex", "Swimming"),
                    i => i.MatchCall("ExtEnum`1<Player/BodyModeIndex>", "op_Inequality")
                    );

                // REPLACE: if (base.bodyChunks[0].ContactPoint.x != 0 ... )
                // WITH:    if (base.bodyChunks[0].ContactPoint.x != 0 && (!Options.fastWallSlide.Value || player.input[0].y > -1) ... )
                c.GotoNext(i => i.MatchLdarg(0));
                c.GotoNext();
                c.RemoveRange(5);
                c.EmitDelegate<Func<Player, bool>>(player => {
                    return player.bodyChunks[0].ContactPoint.x != 0 && (!Options.fastWallSlide.Value || player.input[0].y > -1);
                });
                
                // repeat again with bast.bodyChunks[1]
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

        // AUTO LEDGE CLIMB
        private static void Player_MovementUpdateIL(ILContext il)
        {
            ILCursor c = new(il);

            try
            {
                // REPLACE: else if (canWallJump != 0 && wantToJump > 0 && input[0].x != -Math.Sign(canWallJump))
                // WITH:    see below delegate

                // ... canWallJump != 0 ...
                c.GotoNext(
                    i => i.MatchLdarg(0),
                    i => i.MatchLdfld("Player", "canWallJump")
                    );
                c.GotoNext();
                c.Remove(); // IL_27bd: ldfld int32 Player::canWallJump
                c.EmitDelegate<Func<Player, bool>>(p =>
                {
                    return p.canWallJump != 0 && (
                        // copied vanilla logic. I'm not sure what it does
                        p.wantToJump > 0 && p.input[0].x != -Math.Sign(p.canWallJump)
                        // auto wall climb check
                        || Options.autoWallClimbMode.Value switch { 0 => false, 1 => true, 2 => p.input[0].jmp, 3 => p.input[0].y >= 1, _ => false } // config check
                            && p.input[0].x != 0 && p.bodyChunks[0].ContactPoint.x == p.input[0].x && p.input[0].y >= 0 // pushing against wall check
                            && p.bodyChunks[0].vel.y + p.bodyChunks[1].vel.y < 6f // y velocity check
                            && p.bodyMode == Player.BodyModeIndex.WallClimb && p.IsTileSolid(0, p.input[0].x, 0) && !p.IsTileSolid(0, p.input[0].x, 1) // climbable wall check
                            && !(p.IsTileSolidOrSlope(1, 0f, -12f) || p.IsTileSolidOrSlope(0, 0f, -12f)) // not on the ground check
                        );
                });

                int start = c.Index + 1; // IL_27c4: ldarg.0
                c.GotoNext(i => i.Match(OpCodes.Beq_S)); // end of if statement
                int end = c.Index + 1; // IL_27ea: beq.s IL_2806 (inclusive)
                c.Goto(start);
                c.RemoveRange(end - start);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError(e.Message + "\n" + e.StackTrace);
            }
        }

        // WALL JUMP FIXES
        private static void Player_WallJumpIL(ILContext il)
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
    }
}
