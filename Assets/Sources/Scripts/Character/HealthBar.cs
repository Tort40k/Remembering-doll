using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image _healthBarFilling;
    [SerializeField] private Image _damageTrailFilling;
    [SerializeField] private Health _health;
    [SerializeField] private Gradient _gradient;
    [SerializeField] private float _fillSpeed = 2.5f;
    [SerializeField] private float _trailDelay = 0.35f;
    [SerializeField] private float _trailSpeed = 0.6f;

    private EventBus _eventBus;
    private TextMeshProUGUI _healthText;
    private float _targetNormalized = 1f;
    private float _trailDelayTimer;

    [Inject]
    public void Construct(EventBus eventBus, TextMeshProUGUI healthText)
    {
        _eventBus = eventBus;
        _healthText = healthText;
        
        Debug.Log(this + " is active");
    }

    private void Start()
    {
        if (_eventBus != null)
            _eventBus.Subscribe<HealthChangedSignal>(OnHealthChanged, priority: 10);
    }

    private void OnDisable()
    {
        if (_eventBus != null)
            _eventBus.Unsubscribe<HealthChangedSignal>(OnHealthChanged);
    }

    private void Update()
    {
        if (_healthBarFilling == null) return;

        _healthBarFilling.fillAmount = Mathf.MoveTowards(
            _healthBarFilling.fillAmount,
            _targetNormalized,
            _fillSpeed * Time.deltaTime
        );

        if (_gradient != null)
            _healthBarFilling.color = _gradient.Evaluate(_healthBarFilling.fillAmount);

        if (_damageTrailFilling == null) return;

        if (_healthBarFilling.fillAmount > _damageTrailFilling.fillAmount)
        {
            _damageTrailFilling.fillAmount = _healthBarFilling.fillAmount;
            _trailDelayTimer = 0f;
            return;
        }

        if (_trailDelayTimer > 0f)
        {
            _trailDelayTimer -= Time.deltaTime;
            return;
        }

        if (_damageTrailFilling.fillAmount > _targetNormalized)
        {
            _damageTrailFilling.fillAmount = Mathf.MoveTowards(
                _damageTrailFilling.fillAmount,
                _targetNormalized,
                _trailSpeed * Time.deltaTime
            );
        }
    }

    private void OnHealthChanged(HealthChangedSignal signal)
    {
        _targetNormalized = signal.MaxHealth > 0
            ? Mathf.Clamp01((float)signal.CurrentHealth / signal.MaxHealth): 0f;

        _trailDelayTimer = _trailDelay;

        Debug.Log($"HP {signal.CurrentHealth}/{signal.MaxHealth} ({_targetNormalized:P0})");

        if (_healthText != null)
            _healthText.text = $"HP {signal.CurrentHealth}/{signal.MaxHealth}";
    }
}