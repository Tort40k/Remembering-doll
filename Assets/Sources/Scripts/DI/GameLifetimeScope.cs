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
        builder.RegisterComponentInHierarchy<EventBus>();

        // регистрируем ввод как синглтон
        builder.Register<InputSystem_Actions>(Lifetime.Singleton);

        // безопасная регистрация класса Character по интерфейсу
        builder.RegisterComponentInHierarchy<Character>()
            .As<IControllable>()
            .AsSelf();

        builder.RegisterComponentInHierarchy<CharacterController>();
        builder.RegisterComponentInHierarchy<CharacterInputController>();

        builder.RegisterComponentInHierarchy<Resource>();

        builder.RegisterComponentInHierarchy<Damageable>()
            .As<IDamageable>()
            .AsSelf();

        builder.RegisterComponentInHierarchy<Healable>()
            .As<IHealable>()
            .AsSelf();

        builder.RegisterBuildCallback(container =>
        {
            foreach (var resource in FindObjectsByType<Resource>(FindObjectsSortMode.None))
            {
                container.Inject(resource);
            }

            foreach (var damageable in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                container.Inject(damageable);
            }

            foreach (var healable in FindObjectsByType<Healable>(FindObjectsSortMode.None))
            {
                container.Inject(healable);
            }

            foreach (var bar in FindObjectsByType<ResourceBar>(FindObjectsSortMode.None))
            {
                container.Inject(bar);
            }

            foreach (var damageTest in FindObjectsByType<PlayerDamageTest>(FindObjectsSortMode.None))
            {
                container.Inject(damageTest);
            }

            foreach (var trap in FindObjectsByType<TrapDamageTest>(FindObjectsSortMode.None))
            {
                container.Inject(trap);
            }

            foreach (var healPickup in FindObjectsByType<HealPickup>(FindObjectsSortMode.None))
            {
                container.Inject(healPickup);
            }
        });
    }
}