using System.Collections;
using UnityEngine;
using VContainer;

public class TrapDamageTest : MonoBehaviour
{
    [SerializeField] private int _testNumber = 10;
    [SerializeField] private float _damageInterval = 1.5f;

    private IDamageable _target;
    private Coroutine _damageCoroutine;

    [Inject]
    public void Construct(IDamageable target)
    {
        _target = target;
    }

    private void OnTriggerEnter(Collider other)
{
    if (_target == null || !_target.IsAlive) return;

    if (other.GetComponentInParent<IDamageable>() != _target) return;

    if (_damageCoroutine == null)
        _damageCoroutine = StartCoroutine(DealDamageOverTime());
}

    private void OnTriggerExit(Collider other)
{
    if (other.GetComponentInParent<IDamageable>() != _target) return;

    if (_damageCoroutine != null)
    {
        StopCoroutine(_damageCoroutine);
        _damageCoroutine = null;
    }
}

    private IEnumerator DealDamageOverTime()
    {
        while (_target != null && _target.IsAlive)
        {
            _target.TakeDamage(_testNumber);
            yield return new WaitForSeconds(_damageInterval);
        }

        _damageCoroutine = null;
    }
}