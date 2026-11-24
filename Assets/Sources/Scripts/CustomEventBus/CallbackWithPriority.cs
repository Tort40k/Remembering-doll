
public class CallbackWithPriority
{
    /// Чем выше Priority, тем раньше вызовется ивент
    public readonly int Priority;
    public readonly object Callback;

    public CallbackWithPriority(int priority, object callback)
    {
        Priority = priority;
        Callback = callback;
    }
}