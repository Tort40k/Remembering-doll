using TMPro;
using UnityEngine;
using VContainer;

public class Resource : MonoBehaviour
{
    [SerializeField] private string _resource = "Health";
    [SerializeField] private int _maxValue = 100;
    [SerializeField] private TextMeshProUGUI _resourceText;

    private int _currentValue;
    private EventBus _eventBus;
    private bool _isDead;

    public string ResourceName => _resource;
    public int CurrentValue => _currentValue;
    public int MaxValue => _maxValue;

    [Inject]
    public void Construct(EventBus eventBus)
    {
        _eventBus = eventBus;
        SetValue(_maxValue);

        Debug.Log($"{gameObject.name} -> Resource '{_resource}' injected, EventBus is null: {_eventBus == null}");
    }

    public void Modify(int delta)
    {
        if (_isDead && _resource == "Health") return;
        SetValue(_currentValue + delta);
    }

    private void SetValue(int newValue)
    {
        _currentValue = Mathf.Clamp(newValue, 0, _maxValue);

        if (_resourceText != null)
            _resourceText.text = $"{_resource}: {_currentValue}/{_maxValue}";

        _eventBus?.Invoke(new ResourceChangedSignal(_resource, _currentValue, _maxValue));

        if (_resource == "Health" && _currentValue <= 0 && !_isDead)
        {
            _isDead = true;
            Death();
        }
        else if (_resource == "Health")
        {
            Debug.Log($"{gameObject.name} HP: {_currentValue}");
        }
        else if (_resource == "Armor")
        {
            Debug.Log($"{gameObject.name} Armor: {_currentValue}");
        }
    }

    private void Death()
    {
        Debug.Log($"{gameObject.name} погиб");
    }
}