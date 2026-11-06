# Анимация атаки игрока — отдельный бихейвер

**Дата:** 2026-11-06
**Статус:** утверждено, сделано

## Что было не так

`CharacterFireAction` (атака) сам проигрывал анимацию: после выстрела вызывал `animator.Play` —
`Fire` на слое 1 для огнестрела, `Melee` на слое 2 для ближнего боя. Анимации смерти, движения и
атаки врага живут в бихейверах в `Animation/`; анимацию атаки игрока по дереву было не найти.

## Решение

- Новый `Character/CharacterAttack/Animation/CharacterAttackAnimBehaviour`: подписан на `FireEvent`
  оружия в руках (как `EnemyCharacterAttackAnimBehaviour`), перескакивает на новое оружие при смене,
  играет `Fire`/`Melee` на своих слоях с проверкой `layerCount`.
- Ставится в `PlayerCharacterAnimInstaller`. Врагу и Regax не ставится: у их контроллеров этих слоёв
  нет, и раньше проверка слоя там всё равно глушила вызов.
- `CharacterFireAction` — только атака: проверить `FireCondition`, вызвать `FireAction` оружия.

Тайминг не меняется: оба вида оружия поднимают `FireEvent` внутри того же вызова `FireAction`
(`RangeWeaponFireAction` и `InlineAction` в `MeleeWeaponInstaller`).

## Проверка

Компиляция чистая. В Play на `Playground`: у `[Character]` бихейвер установлен; атака ножом
(`AC_Knife_01`) — слой 2 в состоянии `Melee`; с `AR_01` в руках по `FireEvent` — слой 1 в `Fire`.
Ошибок в консоли нет.
