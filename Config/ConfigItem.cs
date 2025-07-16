namespace EasyLightLevels.Config
{
    public class ConfigItem<T>
    {
        private readonly bool _showDefault;
        private readonly T _trueDefault;
        public readonly string Description;

        private T _val;

        public ConfigItem(T @default, string description, bool showDefault = true)
        {
            _trueDefault = @default;
            Value = @default;
            Description = description;
            _showDefault = showDefault;
        }

        public T Value
        {
            get => _val != null ? _val : Default;
            set => _val = value != null ? value : Default;
        }

        private T Default => _showDefault ? _trueDefault : default;
    }
}