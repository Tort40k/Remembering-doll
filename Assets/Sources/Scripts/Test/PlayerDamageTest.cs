using UnityEngine;
using VContainer;

public class PlayerDamageTest : MonoBehaviour
{
    [SerializeField] private int _testNumber = 10;

    private IDamageable _damageable;
    private IHealable _healable;

    [Inject]
    public void Construct(IDamageable damageable, IHealable healable)
    {
        _damageable = damageable;
        _healable = healable;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            _damageable?.TakeDamage(_testNumber);
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            _healable?.Heal(_testNumber);
        }
    }
}