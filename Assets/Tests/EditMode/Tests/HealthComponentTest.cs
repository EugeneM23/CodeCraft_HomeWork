using System.Collections.Generic;
using Gameplay;
using NUnit.Framework;
using UnityEngine;

public class CharacterTests
{
    [TestCaseSource(nameof(GetTakeDamageCases))]
    public int  TakeDamage(int damage)
    {
        //Arrange
        HealthComponent healthComponent = new HealthComponent(100);
        MoveComponent moveComponent = new MoveComponent();
        Weapon weapon = new GameObject().AddComponent<Weapon>();
        Character character = new GameObject().AddComponent<Character>();

        character.Construct(moveComponent, healthComponent, weapon);

        //Act
        character.TakeDamage(damage);

        return character.CurrentHealth;
    }

    public static IEnumerable<TestCaseData> GetTakeDamageCases()
    {
        yield return new TestCaseData(0).Returns(100);
        yield return new TestCaseData(30).Returns(70);
        yield return new TestCaseData(100).Returns(0);
        yield return new TestCaseData(-20).Returns(100);
    }
}