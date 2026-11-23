using TMPro;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private string _helloText;
    [SerializeField] private TextMeshProUGUI _healthtext;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_helloText);
        builder.RegisterInstance(_healthtext);

        builder.RegisterComponentInHierarchy<TestHello>();

        builder.RegisterComponentInHierarchy<EventBus>();

        builder.RegisterComponentInHierarchy<Health>();
        builder.RegisterComponentInHierarchy<HealthBar>();

        // регистрируем ввод как синглтон
        builder.Register<InputSystem_Actions>(Lifetime.Singleton);

        // безопасная регистрация класса Character по интерфейсу
        builder.RegisterComponentInHierarchy<Character>()
            .As<IControllable>();

        builder.RegisterComponentInHierarchy<CharacterController>();
        builder.RegisterComponentInHierarchy<CharacterInputController>();
    }
}