using Menu.Remix.MixedUI;
using UnityEngine;

namespace MovementTweaks
{
    public class Options : OptionInterface
    {
        public static Options instance = new();

        public static Configurable<bool> fastWallSlide = instance.config.Bind<bool>("MovementTweaks_FastWallSlide", false);
        public static Configurable<int> autoWallClimbMode = instance.config.Bind<int>("MovementTweaks_AutoWallClimb", 0);

        public override void Initialize()
        {
            base.Initialize();

            Tabs = new OpTab[1];
            Tabs[0] = new OpTab(this, "Options");

            AddButton(360, fastWallSlide, "Fast Wall Slide", "Fast Wall Slide - Holding down will prevent you from sticking to walls, causing you to fall faster.");

            float y = 300;
            OpRadioButtonGroup autoWallClimbSelect = new OpRadioButtonGroup(autoWallClimbMode);
            Tabs[0].AddItems(autoWallClimbSelect);
            autoWallClimbSelect.SetButtons(new OpRadioButton[]
            {
                new OpRadioButton(69, y - 25) { description = "Auto Ledge Climb - Off" },
                new OpRadioButton(149, y - 25) { description = "Auto Ledge Climb - On - You will automatically jump to climb up ledges when moving into them and mid-air." },
                new OpRadioButton(229, y - 25) { description = "Auto Ledge Climb - Hold Jump - You will automatically jump to climb up ledges when moving into them, mid-air, and holding jump." },
                new OpRadioButton(309, y - 25) { description = "Auto Ledge Climb - Hold Up - You will automatically jump to climb up ledges when moving into them, mid-air, and holding up." }
            });
            Tabs[0].AddItems(
                new OpLabel(new(40, y), new(80, 20), "Off", FLabelAlignment.Center),
                new OpLabel(new(120, y), new(80, 20), "On", FLabelAlignment.Center),
                new OpLabel(new(200, y), new(80, 20), "Hold Jump", FLabelAlignment.Center),
                new OpLabel(new(280, y), new(80, 20), "Hold Up", FLabelAlignment.Center),
                new OpLabel(new(380, y - 20), new(0, 30), "Auto Ledge Climb", FLabelAlignment.Left, true)
                );
        }

        private void AddButton(float y, Configurable<bool> config, string label, string description)
        {
            Tabs[0].AddItems(
                new OpLabel(new(380, y + 4), Vector2.zero, label, FLabelAlignment.Left, true),
                new OpCheckBox(config, new(309, y)) { description = description }
                );
        }
    }
}
