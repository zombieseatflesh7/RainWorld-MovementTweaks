using Menu.Remix.MixedUI;
using UnityEngine;

namespace MovementTweaks
{
    public class Options : OptionInterface
    {
        public static Options instance = new();

        public static Configurable<bool> fastWallSlide = instance.config.Bind<bool>("MovementTweaks_FastWallSlide", true);

        public override void Initialize()
        {
            base.Initialize();

            Tabs = new OpTab[1];
            Tabs[0] = new OpTab(this, "Options");

            Tabs[0].AddItems(
                new OpLabel(new(300, 300), Vector2.zero, "Fast wall slide", FLabelAlignment.Right),
                new OpCheckBox(fastWallSlide, new Vector2(332, 300 - 4)) { description = "Holding down will prevent you from sticking to walls" }
                );
        }
    }
}
