namespace EasyLightLevels.Config
{
    public class EllConfig
    {
        public readonly ConfigItem<bool> AsSphere = new ConfigItem<bool>(true,
            "Whether to show light levels in a sphere or in a cube. " +
            "Set to true to show light levels in a sphere around you. " +
            "Set to false to show light levels in a cube around you."
        );

        public readonly ConfigItem<bool> ColorContrast = new ConfigItem<bool>(false,
            "Support for better color contrast. Red becomes blue and yellow becomes cyan." +
            "True = enabled, False = disabled. Default = False."
        );

        public readonly ConfigItem<int> Radius = new ConfigItem<int>(32,
            "Radius of the shown light levels in blocks, not including your own position. " +
            "At high values, FPS is not impacted, but the light levels will not be updated smoothly."
        );
    }
}