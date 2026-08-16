using Menu.Remix.MixedUI;
using UnityEngine;

namespace MovementTweaks;

public class Options : OptionInterface
{
    public static Options instance = new();

    //public static Configurable<bool> wallJumpFixes = instance.config.Bind<bool>("MovementTweaks_WallJumpFixes", true);
    public static Configurable<bool> pullupFixes = instance.config.Bind<bool>("MovementTweaks_PullupFixes", true);
    public static Configurable<int> pullupDirectionMode = instance.config.Bind<int>("MovementTweaks_PullupDirectionMode", 0);
    public static Configurable<bool> fastWallSlide = instance.config.Bind<bool>("MovementTweaks_FastWallSlide", false);
    public static Configurable<int> autoWallClimbMode = instance.config.Bind<int>("MovementTweaks_AutoWallClimb", 0);
    
    public override void Initialize()
    {
        base.Initialize();

        Tabs = new OpTab[1];
        Tabs[0] = new OpTab(this, "Options");

        Tabs[0].AddItems(
            new OpLabel(new(300, 560), new(0, 0), "Movement Tweaks", FLabelAlignment.Center, true),
            new OpLabel(new(300, 540), new(0, 0), "By Zombieseatflesh7", FLabelAlignment.Center),
            new OpLabel(new(300, 0), new(0, 0), "See the mod page for more information", FLabelAlignment.Center)
            );

        //AddButton(450, wallJumpFixes, "Wall Jump Fixes", "Wall Jump Fixes - Includes the standing wall jump and sloped wall jump fixes from the mod page.");
        AddButton(375, pullupFixes, "Pullup Fixes", "Pullup Fixes - Rewritten pullup physics and collision detection.");
        
        float y;
        y = 300;
        OpRadioButtonGroup pullupDirectionSelect = new OpRadioButtonGroup(pullupDirectionMode);
        Tabs[0].AddItems(pullupDirectionSelect);
        pullupDirectionSelect.SetButtons(new OpRadioButton[]
        {
            new OpRadioButton(69, y - 4) { description = "Pullup Direction - Vanilla - Always prefer horizontal pullups." },
            new OpRadioButton(189, y - 4) { description = "Pullup Direction - Manual - Prefer horizontal pullups when the player is moving, otherwise use vertical pullups - Requires Pullup Fixes" },
            new OpRadioButton(309, y - 4) { description = "Pullup Direction - Only Vertical - Only do vertical pullups - Requires Pullup Fixes" },
        });
        Tabs[0].AddItems(
            new OpLabel(new(40, y + 20), new(80, 20), "Vanilla", FLabelAlignment.Center),
            new OpLabel(new(160, y + 20), new(80, 20), "Manual", FLabelAlignment.Center),
            new OpLabel(new(280, y + 20), new(80, 20), "Only Vertical", FLabelAlignment.Center),
            new OpLabel(new(380, y), new(0, 0), "Pullup Direction", FLabelAlignment.Left, true)
            );

        AddButton(225, fastWallSlide, "Fast Wall Slide", "Fast Wall Slide - Holding down will prevent you from sticking to walls, causing you to fall faster.");

        y = 150;
        OpRadioButtonGroup autoWallClimbSelect = new OpRadioButtonGroup(autoWallClimbMode);
        Tabs[0].AddItems(autoWallClimbSelect);
        autoWallClimbSelect.SetButtons(new OpRadioButton[]
        {
            new OpRadioButton(69, y - 4) { description = "Auto Ledge Climb - Off" },
            new OpRadioButton(149, y - 4) { description = "Auto Ledge Climb - On - You will automatically jump to climb up ledges when moving into them and mid-air." },
            new OpRadioButton(229, y - 4) { description = "Auto Ledge Climb - Hold Jump - You will automatically jump to climb up ledges when moving into them, mid-air, and holding jump." },
            new OpRadioButton(309, y - 4) { description = "Auto Ledge Climb - Hold Up - You will automatically jump to climb up ledges when moving into them, mid-air, and holding up." }
        });
        Tabs[0].AddItems(
            new OpLabel(new(40, y + 20), new(80, 20), "Off", FLabelAlignment.Center),
            new OpLabel(new(120, y + 20), new(80, 20), "On", FLabelAlignment.Center),
            new OpLabel(new(200, y + 20), new(80, 20), "Hold Jump", FLabelAlignment.Center),
            new OpLabel(new(280, y + 20), new(80, 20), "Hold Up", FLabelAlignment.Center),
            new OpLabel(new(380, y), new(0, 0), "Auto Ledge Climb", FLabelAlignment.Left, true)
            );

        
    }

    private void AddButton(float y, Configurable<bool> config, string label, string description)
    {
        Tabs[0].AddItems(
            new OpLabel(new(380, y), Vector2.zero, label, FLabelAlignment.Left, true),
            new OpCheckBox(config, new(309, y - 4)) { description = description }
            );
    }
}
