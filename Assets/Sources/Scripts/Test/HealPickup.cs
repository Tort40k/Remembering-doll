using System.Collections;
using UnityEngine;
using VContainer;

public class HealPickup : MonoBehaviour
{
    [SerializeField] private int _healAmount = 25;
    [SerializeField] private bool _destroyOnPickup = true;
    [SerializeField] private float _respawnDelay = 10f;

    private IHealable _target;
    private bool _isConsumed;

    [Inject]
    public void Construct(IHealable target)
    {
        _target = target;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isConsumed || _target == null || !_target.CanHeal) return;

        _target.Heal(_healAmount);
        _isConsumed = true;

        if (_destroyOnPickup)
        {
            Destroy(gameObject);
        }
        else if (_respawnDelay > 0f)
        {
            StartCoroutine(RespawnRoutine());
        }
    }

    private IEnumerator RespawnRoutine()
    {
        SetVisualsAndColliderActive(false);
        yield return new WaitForSeconds(_respawnDelay);
        _isConsumed = false;
        SetVisualsAndColliderActive(true);
    }

    private void SetVisualsAndColliderActive(bool active)
    {
        foreach (var rend in GetComponentsInChildren<Renderer>())
            rend.enabled = active;

        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = active;
    }
}