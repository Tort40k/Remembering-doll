using UnityEngine;
using VContainer;

public class Damageable : MonoBehaviour, IDamageable
{
    [SerializeField] private string _healthResourceName = "Health";
    [SerializeField] private string _armorResourceName = "Armor";
    [SerializeField] private bool _armorAbsorbsAllFirst = true;

    private Resource _health;
    private Resource _armor;

    public bool IsAlive => _health != null && _health.CurrentValue > 0;

    [Inject]
    public void Construct()
    {
        foreach (var resource in GetComponents<Resource>())
        {
            if (resource.ResourceName == _healthResourceName)
                _health = resource;
            else if (resource.ResourceName == _armorResourceName)
                _armor = resource;
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || !IsAlive) return;

        int remainingDamage = amount;

        if (_armor != null && _armor.CurrentValue > 0)
        {
            if (_armorAbsorbsAllFirst)
            {
                int absorbed = Mathf.Min(_armor.CurrentValue, remainingDamage);
                _armor.Modify(-absorbed);
                remainingDamage -= absorbed;
            }
            else
            {
                int armorDamage = Mathf.Max(1, remainingDamage / 2);
                int absorbed = Mathf.Min(_armor.CurrentValue, armorDamage);
                _armor.Modify(-absorbed);
                remainingDamage -= absorbed;
            }
        }

        if (remainingDamage > 0 && _health != null)
        {
            _health.Modify(-remainingDamage);
        }
    }
}