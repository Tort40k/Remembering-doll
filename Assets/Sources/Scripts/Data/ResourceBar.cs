using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class ResourceBar : MonoBehaviour
{
    [SerializeField] private string _resource = "Health";
    [SerializeField] private TextMeshProUGUI _resourceText;
    [SerializeField] private Image _barFilling;
    [SerializeField] private Image _damageTrailFilling;
    [SerializeField] private Resource _targetResource;
    [SerializeField] private Gradient _gradient;
    [SerializeField] private float _fillSpeed = 2.5f;
    [SerializeField] private float _trailDelay = 0.35f;
    [SerializeField] private float _trailSpeed = 0.6f;

    private EventBus _eventBus;
   
    private float _targetNormalized = 1f;
    private float _trailDelayTimer;

    [Inject]
    public void Construct(EventBus eventBus)
    {
        _eventBus = eventBus;

        Debug.Log(this + " is active");
    }

    private void Start()
    {
        if (_eventBus != null)
            _eventBus.Subscribe<ResourceChangedSignal>(OnResourceChanged, priority: 10);
    }

    private void OnDisable()
    {
        if (_eventBus != null)
            _eventBus.Unsubscribe<ResourceChangedSignal>(OnResourceChanged);
    }

    private void Update()
    {
        if (_barFilling == null) return;

        _barFilling.fillAmount = Mathf.MoveTowards(
            _barFilling.fillAmount,
            _targetNormalized,
            _fillSpeed * Time.deltaTime
        );

        if (_gradient != null)
            _barFilling.color = _gradient.Evaluate(_barFilling.fillAmount);

        if (_damageTrailFilling == null) return;

        if (_barFilling.fillAmount > _damageTrailFilling.fillAmount)
        {
            _damageTrailFilling.fillAmount = _barFilling.fillAmount;
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

    private void OnResourceChanged(ResourceChangedSignal signal)
    {
        if (signal.Resource != _resource) return;

        _targetNormalized = signal.MaxValue > 0
            ? Mathf.Clamp01((float)signal.CurrentValue / signal.MaxValue)
            : 0f;

        _trailDelayTimer = _trailDelay;

        Debug.Log($"{_resource} {signal.CurrentValue}/{signal.MaxValue} ({_targetNormalized:P0})");

        if (_resourceText != null)
            _resourceText.text = $"{_resource} {signal.CurrentValue}/{signal.MaxValue}";
    }
}