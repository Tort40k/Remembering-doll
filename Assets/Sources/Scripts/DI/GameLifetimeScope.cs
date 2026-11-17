using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private string _helloText;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_helloText);
        builder.RegisterComponentInHierarchy<TestHello>();
        
        // Регистрируем ввод как синглтон
        builder.Register<InputSystem_Actions>(Lifetime.Singleton);

        // Безопасная регистрация вашего класса Character по интерфейсу
        builder.RegisterComponentInHierarchy<Character>()
            .As<IControllable>();

        builder.RegisterComponentInHierarchy<CharacterController>();
        builder.RegisterComponentInHierarchy<CharacterInputController>();
    }
}