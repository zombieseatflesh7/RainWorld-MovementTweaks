using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using System;
using UnityEngine;
using static Room.Tile;

namespace MovementTweaks
{
    internal static class Hooks
    {
        // NOTE FOR READERS: IL labels may change between game versions. This does not mean the mod is broken.

        internal static void InitHooks()
        {
            IL.Player.Update += Player_UpdateIL;
            IL.Player.MovementUpdate += Player_MovementUpdateIL;
            IL.Player.WallJump += Player_WallJumpIL;
            IL.Player.UpdateAnimation += Player_UpdateAnimationIL;
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

        private static void Player_MovementUpdateIL(ILContext il)
        {
            ILCursor c = new(il);

            try
            {
                // PULLUP FIX - prevent the player from flipping directions while doing a pullup
                // REPLACE: if (num != flipDirection && num != 0)
                // WITH:    if (p.animation != Player.AnimationIndex.GetUpOnBeam && num != flipDirection && num != 0)

                // if (num != flipDirection && num != 0)
                c.GotoNext(
                    i => i.Match(OpCodes.Ldloc_0),
                    i => i.Match(OpCodes.Ldarg_0),
                    i => i.MatchCall("Player", "get_flipDirection")
                    );
                ILLabel branchDestination = il.Instrs[c.Index + 3].Operand as ILLabel; // IL_00b5 -- if (rippleActivating && CanLevitate)

                // insert this delegate before the other branches
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc_0);
                c.EmitDelegate<Func<Player, int, int>>((p, x) => { return (Options.pullupFixes.Value && p.animation == Player.AnimationIndex.GetUpOnBeam) ? 0 : x; });
                c.Emit(OpCodes.Stloc_0);

                // AUTO LEDGE CLIMB

                // IL_27ba: br.s IL_282d - grabbing this label
                c.GotoNext(
                    i => i.Match(OpCodes.Br_S),
                    i => i.MatchLdarg(0),
                    i => i.MatchLdfld("Player", "canWallJump")
                    );
                ILLabel endOfIfElseChain = c.Next.Operand as ILLabel; // IL_282d

                // IL_27bc: ldarg.0
                // EMIT FUNCTION HERE
                // IL_27bd: ldfld int32 Player::canWallJump
                c.Goto(c.Index + 2);
                c.EmitDelegate<Func<Player, bool>>(p => 
                {
                    if( p.canWallJump != 0 && ( // auto wall climb check
                        Options.autoWallClimbMode.Value switch { 0 => false, 1 => true, 2 => p.input[0].jmp, 3 => p.input[0].y >= 1, _ => false } // config check
                        && p.input[0].x != 0 && p.bodyChunks[0].ContactPoint.x == p.input[0].x && p.input[0].y >= 0 // pushing against wall check
                        && p.bodyChunks[0].vel.y + p.bodyChunks[1].vel.y < 6f // y velocity check
                        && p.bodyMode == Player.BodyModeIndex.WallClimb && p.IsTileSolid(0, p.input[0].x, 0) && !p.IsTileSolid(0, p.input[0].x, 1) // climbable wall check
                        && !(p.IsTileSolidOrSlope(1, 0f, -12f) || p.IsTileSolidOrSlope(0, 0f, -12f)) // not on the ground check
                        ))
                    {
                        p.WallJump(Math.Sign(p.canWallJump));
                        p.wantToJump = 0;
                        return false;
                    }
                    return true;
                });
                c.Emit(OpCodes.Brfalse_S, endOfIfElseChain);
                c.Emit(OpCodes.Ldarg_0);
                // IL_27bd: ldfld int32 Player::canWallJump ...
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

        // PULLUP FIXES
        private static void Player_UpdateAnimationIL(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            try
            {
                // INITIATING PULLUP

                // if (flag) - flag seems to control if we should begin a pullup this frame
                c.GotoNext(i => i.Match(OpCodes.Ldloc_0)); // IL_0a25: ldloc.0
                c.GotoNext(); // IL_0a26: brfalse IL_0bff
                ILLabel branchDestination = c.Next.Operand as ILLabel;
                c.GotoNext();
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<Player, bool>>(p =>
                {
                    if (!Options.pullupFixes.Value)
                        return true;

                    Room room = p.room;
                    Vector2 headPos = p.bodyChunks[0].pos;

                    if (room.GetTile(headPos + new Vector2(0, 20f)).Terrain is TerrainType.Solid or TerrainType.Slope) // block above head - can't do pullup
                        return false;

                    bool valid = false;
                    p.straightUpOnHorizontalBeam = Options.pullupDirectionMode.Value switch
                    {
                        0 => false,
                        1 => p.input[0].x == 0,
                        2 => true,
                        _ => false
                    };
                    p.upOnHorizontalBeamPos = new Vector2(headPos.x, room.MiddleOfTile(headPos).y + 20f); // pullup position affects how the slugcat moves during the animation

                    if (room.GetTile(p.upOnHorizontalBeamPos + new Vector2(-9f, 0f)).Terrain is TerrainType.Solid or TerrainType.Slope) // check if too close to a tile on the left
                        p.upOnHorizontalBeamPos.x = room.MiddleOfTile(p.upOnHorizontalBeamPos).x - 1f;
                    else if (room.GetTile(p.upOnHorizontalBeamPos + new Vector2(10f, 0f)).Terrain is TerrainType.Solid or TerrainType.Slope) // check if too close to a tile on the right
                        p.upOnHorizontalBeamPos.x = room.MiddleOfTile(p.upOnHorizontalBeamPos).x + 1f;

                    if (!p.straightUpOnHorizontalBeam
                        && room.GetTile(headPos + new Vector2(p.flipDirection * 20f, 0f)).horizontalBeam
                        && !(room.GetTile(headPos + new Vector2(p.flipDirection * 20f, 0f)).Terrain is TerrainType.Solid or TerrainType.Slope) // no horizontaly adjacent block or slope
                        && !(room.GetTile(headPos + new Vector2(p.flipDirection * 20f, 20f)).Terrain is TerrainType.Solid or TerrainType.Slope) // no diagonaly adjacent block or slope
                        )
                    {
                        valid = true;
                    }
                    else if (!p.straightUpOnHorizontalBeam
                        && room.GetTile(headPos + new Vector2(-p.flipDirection * 20f, 0f)).horizontalBeam // reversed direction
                        && !(room.GetTile(headPos + new Vector2(-p.flipDirection * 20f, 0f)).Terrain is TerrainType.Solid or TerrainType.Slope) // no horizontaly adjacent block or slope
                        && !(room.GetTile(headPos + new Vector2(-p.flipDirection * 20f, 20f)).Terrain is TerrainType.Solid or TerrainType.Slope) // no diagonaly adjacent block or slope
                        )
                    {
                        p.flipDirection *= -1;
                        valid = true;
                    }
                    else
                    { 
                        p.straightUpOnHorizontalBeam = true;
                        valid = true;
                    } 

                    if (valid)
                    {
                        if (!p.straightUpOnHorizontalBeam) // if horizontal pullup
                            if (!room.GetTile(headPos + new Vector2(p.flipDirection * 25f, 0f)).horizontalBeam // too close to beam edge
                                || room.GetTile(headPos + new Vector2(p.flipDirection * 25f, 0f)).Terrain is TerrainType.Solid or TerrainType.Slope // too close to horizontaly adjacent block or slope
                                || room.GetTile(headPos + new Vector2(p.flipDirection * 25f, 20f)).Terrain is TerrainType.Solid // too close to diagonaly adjacent block
                                )
                                p.upOnHorizontalBeamPos.x = room.MiddleOfTile(p.upOnHorizontalBeamPos).x + 5f * p.flipDirection; // shift back slightly

                        room.PlaySound(SoundID.Slugcat_Get_Up_On_Horizontal_Beam, p.mainBodyChunk, loop: false, 1f, 1f);
                        p.animation = Player.AnimationIndex.GetUpOnBeam;
                        p.pullupSoftlockSafety = 0;
                    }
                    return false;
                });
                // if the last function returns false, skip the vanilla pullup code
                c.Emit(OpCodes.Brfalse, branchDestination);

                // PULLUP ANIMATION
                c.GotoNext(
                    i => i.MatchLdsfld<Player.AnimationIndex>("GetUpOnBeam"),
                    i => i.MatchCall("ExtEnum`1<Player/AnimationIndex>", "op_Equality")
                    );
                c.GotoNext(i => i.MatchLdarg(0));

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<Player, bool>>(p =>
                {
                    if (!Options.pullupFixes.Value)
                        return true;

                    p.pullupSoftlockSafety++;
                    if (p.pullupSoftlockSafety > 80)
                    {
                        Custom.Log("Pullup softlock safety");
                        p.room.PlaySound(SoundID.Slugcat_Turn_In_Corridor, p.mainBodyChunk, loop: false, 1f, 1f);
                        p.pullupSoftlockSafety = 0;
                        p.animation = Player.AnimationIndex.HangFromBeam;
                        return false;
                    }

                    if (p.input[0].y < 0) // press down to cancel pullup
                    {
                        p.pullupSoftlockSafety = 0;
                        p.animation = Player.AnimationIndex.None;
                        return false;
                    }

                    BodyChunk head = p.bodyChunks[0], feet = p.bodyChunks[1];
                    Room room = p.room;

                    p.bodyMode = Player.BodyModeIndex.ClimbingOnBeam;
                    head.vel.x = 0f;
                    head.vel.y = 0f;
                    p.forceFeetToHorizontalBeamTile = 20;

                    if (p.straightUpOnHorizontalBeam) // VERTICAL PULLUP
                    {
                        if (room.GetTile(feet.pos).horizontalBeam && feet.pos.y > p.upOnHorizontalBeamPos.y - 25f) // legs reach beam
                        {
                            // complete pullup
                            p.noGrabCounter = 15;
                            p.animation = Player.AnimationIndex.StandOnBeam;
                            feet.pos.y = p.room.MiddleOfTile(feet.pos).y + 5f;
                            feet.vel.y = 0f;
                        }
                        else if ((!room.GetTile(head.pos).horizontalBeam && !room.GetTile(feet.pos).horizontalBeam) || !Custom.DistLess(head.pos, p.upOnHorizontalBeamPos, 30f)) // not on beam or too far from pullup position
                        {
                            p.animation = Player.AnimationIndex.None; // fall
                        }

                        // move towards pullup position
                        head.vel.y += 3.2f;
                        head.vel.x += Mathf.Clamp(p.upOnHorizontalBeamPos.x - head.pos.x, -1f, 1f);
                        feet.vel.x = Mathf.MoveTowards(feet.vel.x, Mathf.Clamp(p.upOnHorizontalBeamPos.x - feet.pos.x, -1f, 1f), 0.5f);
                    }
                    else // HORIZONTAL PULLUP
                    {
                        head.pos.y = p.room.MiddleOfTile(head.pos).y; // lock head to beam
                        
                        if (feet.ContactPoint.y > 0) // feet collide with something above (this never triggers on blocks, because the pullup counts as complete before it happens)
                        {
                            if (!room.GetTile(head.pos + new Vector2(0f, 20f)).Solid)
                            {
                                p.straightUpOnHorizontalBeam = true;
                            }
                            else
                            {
                                p.animation = Player.AnimationIndex.HangFromBeam;
                            }
                            return false;
                        }
                        if (feet.pos.y > head.pos.y) // feet above head
                        {
                            if (room.GetTile(feet.pos).horizontalBeam && room.GetTile(feet.pos + new Vector2(0, 20)).Terrain is not TerrainType.Solid) // valid pullup position
                            {
                                // complete pullup
                                p.noGrabCounter = 15;
                                p.animation = Player.AnimationIndex.StandOnBeam;
                                feet.pos.y = p.room.MiddleOfTile(head.pos).y + 5f;
                                feet.vel.y = 0f;
                                head.vel.y = 2f;
                            }
                            else
                            {
                                p.animation = Player.AnimationIndex.HangFromBeam;
                            }
                            return false;
                        }
                        if (!room.GetTile(head.pos).horizontalBeam)
                        {
                            p.pullupSoftlockSafety = 0;
                            p.animation = Player.AnimationIndex.None;
                            return false;
                        }

                        head.vel.x += Mathf.Clamp(p.upOnHorizontalBeamPos.x - head.pos.x, -2f, 2f); // move head towards pullup position

                        // raise legs
                        // for context: 17 is the distance between a slugcats head and legs bodychunks
                        // the purpose of this math is to make sure the legs travel in a perfect circular arc, and prevent them from pulling the scug left or right
                        Vector2 pivot = new Vector2(p.upOnHorizontalBeamPos.x, head.pos.y);
                        float angle1 = Mathf.Atan2(feet.pos.y - pivot.y, feet.pos.x - pivot.x); // angle from pivot to legs
                        float angle2 = angle1 + (5f / 17f) * p.flipDirection; // angle that slugcat would be at if it's legs moved 5 units
                        float distance = (feet.pos - pivot).magnitude; // distance from pivot to legs
                        float angle = Mathf.Atan2(17f*Mathf.Sin(angle2) - distance*Mathf.Sin(angle1) + p.gravity, 17f*Mathf.Cos(angle2) - distance*Mathf.Cos(angle1)); // angle of force
                        feet.vel = Vector2.MoveTowards(feet.vel, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5f, 2f); // apply force to legs
                    }
                    return false;
                });

                // if the last function returns false, skip the vanilla pullup code
                ILLabel label = c.DefineLabel(); 
                c.Emit(OpCodes.Brtrue, label);
                c = c.Emit(OpCodes.Ret);
                c.MarkLabel(label);

            } 
            catch (Exception e)
            {
                Plugin.Logger.LogError(e.Message + "\n" + e.StackTrace);
            }
        }
    }
}
