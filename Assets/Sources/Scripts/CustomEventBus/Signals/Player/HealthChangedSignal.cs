public class HealthChangedSignal
{
    public readonly int CurrentHealth;
    public readonly int MaxHealth;

    public HealthChangedSignal(int currentHealth, int maxHealth)
    {
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
    }
}