public class ResourceChangedSignal
{
    public readonly string Resource;
    public readonly int CurrentValue;
    public readonly int MaxValue;

    public ResourceChangedSignal(string resource, int currentValue, int maxValue)
    {
        Resource = resource;
        CurrentValue = currentValue;
        MaxValue = maxValue;
    }
}