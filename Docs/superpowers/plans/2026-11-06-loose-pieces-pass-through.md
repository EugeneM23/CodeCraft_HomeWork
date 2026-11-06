# Танк и персонажи сквозь осколки — план

Спек: `Docs/superpowers/specs/2026-11-06-loose-pieces-pass-through-design.md`.

Ограничения сессии: код не коммитится (в нём незакоммиченные правки пользователя), только документы;
редактор общий — компилировать только вне Play.

1. **Тесты (красные).** `Assets/Game/Tests/PassThroughLoosePiecesTests.cs`:
   `IsLoose` для динамического тела, кинематического, коллайдера без тела, коллайдера под
   динамическим корнем. Объекты создаются и уничтожаются в каждом тесте.
2. **Слои.** `PhysicsLayers.DestructiblePieces = (1 << 14) | (1 << 15) | (1 << 16)`.
3. **Поведение.** `Assets/Game/Scripts/Features/Destruction/PassThroughLoosePiecesBehaviour.cs`
   по спеку: `IsLoose`, Init собирает не-триггерные коллайдеры, FixedTick — запрос и
   `GetIgnoreCollision`/`IgnoreCollision`.
4. **Подключение.** `TankInstaller`, `CharacterCoreInstaller`, `EnemyCoreInstaller` —
   `entity.AddBehaviour(new PassThroughLoosePiecesBehaviour())`.
5. **Компиляция вне Play, тесты.** Все EditMode-тесты зелёные.
6. **Play.** Пролом стены рядом с танком и игроком; проезд и проход через завал. Замеры: крен корпуса,
   смещение кусков от касания, число пар с отключёнными столкновениями; упор в стоячую стену и таран.
7. **Спек — замеры, коммит документов.**
