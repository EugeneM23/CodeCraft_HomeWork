# Флеш-граната — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** второй тип гранаты — флешка, ослепляющая врагов и белящая экран игрока; переключение типов по Tab.

**Architecture:** запас гранат на персонаже становится словарём по `GrenadeType`; бросок и запал общие,
«начинка» гранаты — `ExplosionInstaller` или новый `FlashInstaller`. Враги слепнут через `BlindBuff`,
игрок получает `FlashedEvent`, на который отвечает экранный `FlashScreenBehaviour`. Префабы, бафф,
Canvas и подключение собирает `GrenadeBuilder`.

**Tech Stack:** Unity 6000.0.38f1, Atomic Entities, uGUI (`UnityEngine.UI.Text/Image`).

Спек: `Docs/superpowers/specs/2026-11-05-flash-grenade-design.md`.

## Global Constraints

- Числа из спека: Frag 3 / Flash 2, Tab, радиус 8 м, запал 1.5 с, Blind 4 с, StumbleSpeed 0.35,
  сила `1 − 0.7·d/r`, удержание `0.3 + 0.7·s`, затухание `1 + 2.5·s`.
- Тесты не пишутся (решение пользователя для гранат), проверка — Play mode.
- Без коммитов. Установщик действует только из `MonoEntity.sceneInstallers`.

### Task 1: Типы и переключение
- [ ] `GrenadeType`; ключи `GrenadeAmmo` (словарь), `GrenadePrefabs`, `GrenadeSelected`, `GrenadeSwitchAction`.
- [ ] `CharacterGrenadeInstaller` со слотами; `GrenadeThrowAction` берёт тип/префаб/запас из сущности;
      `GrenadeSwitchAction`; Tab в `CharacterGrenadeController`; `GrenadePickUpInstaller._type`;
      `GrenadeHudBehaviour`.

### Task 2: Флешка
- [ ] Ключи `FlashRadius`, `FlashBlindBuff`, `FlashVfxPrefab`, `FlashedEvent`.
- [ ] `FlashInstaller`, `FlashUseCase`, ветка в `GrenadeFuseBehaviour`.
- [ ] `BlindBuffConfig`, `BlindBuff`, `BlindStumbleBehaviour`.

### Task 3: Экран игрока
- [ ] `CharacterFlashInstaller`, `FlashScreenBehaviour`.

### Task 4: Сборка
- [ ] `GrenadeBuilder`: `BlindBuffConfig.asset`, `FX_Flashbang`, `Grenade_Flash_01`, `FlashGrenadeItem`,
      слоты и `<FlashScreen>` на `[Character]` (префаб и сцена), пикапы на Playground;
      `PlaygroundBuilder` — `FlashGrenadeItem` в списке; `Game.Editor` → `UnityEngine.UI`.

### Task 5: Проверка в Play mode (см. спек).
