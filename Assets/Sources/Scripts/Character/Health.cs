using TMPro;
using UnityEngine;
using VContainer;

public class Health : MonoBehaviour
{
    [SerializeField] private int _maxHealth = 100;
    private int _currenthealth;
    private TextMeshProUGUI _healthText;
    private EventBus _eventBus;
    private bool _isDead;
    
    [Inject]
    public void Construct(EventBus eventBus, TextMeshProUGUI healthText)
    {
        _healthText = healthText;
        _eventBus = eventBus;
        _currenthealth = _maxHealth;

        _healthText.text = $"HP {_currenthealth}/{_maxHealth}";

        Debug.Log(this + " is active");
    }
   
    private void Update()
    {
        // Тест урона по нажатию F
        if (Input.GetKeyDown(KeyCode.F))
        {
            TakeDamage(5);
        }

        // Тест лечения по нажатию H
        if (Input.GetKeyDown(KeyCode.H))
        {
            Heal(15);
        }
    }
    private void TakeDamage(int amount)
    {
        if(amount <= 0 || _isDead) return; 
        ChangeHealth(-amount);
    }

    private void Heal(int amount)
    {
        if(amount <= 0 || _isDead) return; 
        ChangeHealth(amount);
    }

    private void ChangeHealth(int value)
    {
        _currenthealth = Mathf.Clamp(_currenthealth + value, 0, _maxHealth);

        _eventBus?.Invoke(new HealthChangedSignal(_currenthealth, _maxHealth));
        if (_currenthealth <= 0 && !_isDead)
        {
            _isDead = true;
            Death();
        }
    }

    private void Death()
    {
        Debug.Log($"{gameObject.name} погиб");

    }
}
