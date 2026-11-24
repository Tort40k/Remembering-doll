using UnityEngine;
using VContainer;

public class Healable : MonoBehaviour, IHealable
{
    private Resource _health;
    private IDamageable _damageable;

    public bool CanHeal =>
        _health != null &&
        _health.CurrentValue < _health.MaxValue &&
        (_damageable == null || _damageable.IsAlive);

    [Inject]
    public void Construct(Resource health, IDamageable damageable)
    {
        _health = health;
        _damageable = damageable;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || !CanHeal) return;

        _health.Modify(amount);
    }
}