using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIKit.Editor
{
    /// <summary>
    /// The icon of a node on the canvas, drawn as an ability slot of a game: a coloured tile with a lighter upper half
    /// (a shine) and a dark rim, and on it a solid white silhouette of what the node does, with a shadow under it
    /// (a sword — attack, an eye — a sensor, an hourglass — wait). The colour says the kind of the action: red — a
    /// blow, blue — movement, purple — the group, amber — senses, grey — the rest.
    /// Drawn in code (Painter2D): no image assets. A node type without a silhouette of its own gets no icon.
    /// </summary>
    internal static class NodeIcons
    {
        /// <summary>The side of the tile, pixels; the silhouettes are drawn on a 24 × 24 grid.</summary>
        private const float SIZE = 26f;

        private const float GRID = 24f;

        /// <summary>A kind of action: the rim (and the shadow), the tile, its shine.</summary>
        private readonly struct Palette
        {
            public readonly Color Rim;
            public readonly Color Tile;
            public readonly Color Shine;

            public Palette(int rim, int tile, int shine)
            {
                Rim = Hex(rim);
                Tile = Hex(tile);
                Shine = Hex(shine);
            }

            private static Color Hex(int rgb) =>
                new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }

        private static readonly Palette Blow = new(0x5E1D19, 0xB93D34, 0xCF5348);
        private static readonly Palette Movement = new(0x173A66, 0x2F6FC4, 0x4285D6);
        private static readonly Palette Team = new(0x3A2466, 0x7A52C4, 0x8F69D6);
        private static readonly Palette Senses = new(0x6B4710, 0xC98A22, 0xDBA037);
        private static readonly Palette Other = new(0x2F3136, 0x63666D, 0x787B83);
        private static readonly Palette Work = new(0x1F4D2B, 0x3E9A55, 0x52AE69);

        private static readonly Dictionary<string, (Action<Painter2D> Draw, Palette Colors)> ById = new()
        {
            { "aikit.task.attack", (Sword, Blow) },
            { "aikit.task.switch-weapon", (Sword, Other) },
            { "aikit.task.combat-brain", (CrossedSwords, Blow) },
            { "aikit.task.use-ability", (Spark, Blow) },
            { "aikit.task.move-to", (Arrow, Movement) },
            { "aikit.task.chase", (Chevrons, Movement) },
            { "aikit.task.patrol-area", (BackAndForth, Movement) },
            { "aikit.task.circle-target", (Orbit, Movement) },
            { "aikit.task.kite", (Distance, Movement) },
            { "aikit.task.take-attack-slot", (Tick, Team) },
            { "aikit.task.release-attack-slot", (Minus, Team) },
            { "aikit.task.call-for-help", (Megaphone, Team) },
            { "aikit.task.perform", (Hammer, Work) },
            { "aikit.task.chat", (Talk, Team) },
            { "aikit.task.wait", (Hourglass, Other) },
            { "aikit.task.debug-log", (Bubble, Other) }
        };

        /// <summary>The icon of a state: by the task it runs (its first foreground one); null — no icon for it.</summary>
        public static VisualElement For(AgentState state)
        {
            if (state == null)
                return null;

            foreach (AgentTask task in state.Tasks)
            {
                if (task == null || task.IsBackground)
                    continue;

                // A move that needs a clear line is a shot — known only from the weapon of the character shown
                if (task is AttackTask attack && ElementInspector.Character != null &&
                    attack.NeedsSightWith(ElementInspector.Character.Weapon))
                    return Slot(Bow, Blow);

                if (task is FlankTask)
                    return Slot(Around, Movement);      // a chase that comes up from its own side

                var info = (AIKitNodeAttribute)Attribute.GetCustomAttribute(task.GetType(), typeof(AIKitNodeAttribute));
                return info != null && ById.TryGetValue(info.Id, out (Action<Painter2D> Draw, Palette Colors) icon)
                    ? Slot(icon.Draw, icon.Colors)
                    : null;
            }

            return null;
        }

        /// <summary>The icon of a sensor: an eye.</summary>
        public static VisualElement ForSensor() => Slot(Eye, Senses);

        // The tile: a dark rim, the coloured face inside it, a lighter upper half, and the silhouette painted on top
        private static VisualElement Slot(Action<Painter2D> draw, Palette colors)
        {
            var slot = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.style.width = slot.style.minWidth = SIZE;
            slot.style.height = slot.style.minHeight = SIZE;
            slot.style.flexShrink = 0f;
            slot.style.alignSelf = Align.Center;
            slot.style.marginLeft = 6f;
            slot.style.marginRight = 2f;
            slot.style.backgroundColor = colors.Rim;
            Round(slot, 5f, 5f);

            VisualElement face = Layer(2f, 2f, 2f, 2f);
            face.style.backgroundColor = colors.Tile;
            Round(face, 3.5f, 3.5f);
            slot.Add(face);

            VisualElement shine = Layer(2f, 2f, 2f, SIZE * 0.5f);
            shine.style.backgroundColor = colors.Shine;
            Round(shine, 3.5f, 0f);
            slot.Add(shine);

            VisualElement picture = Layer(0f, 0f, 0f, 0f);
            picture.generateVisualContent += context =>
            {
                Painter2D painter = context.painter2D;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;

                // The shadow first, a little down and to the right, then the silhouette itself
                Begin(colors.Rim, colors.Rim, 0.7f, 0.9f);
                draw(painter);
                Begin(Color.white, colors.Tile, 0f, 0f);
                draw(painter);
            };
            slot.Add(picture);
            return slot;
        }

        private static VisualElement Layer(float left, float top, float right, float bottom)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = left;
            layer.style.top = top;
            layer.style.right = right;
            layer.style.bottom = bottom;
            return layer;
        }

        private static void Round(VisualElement element, float top, float bottom)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = top;
            element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = bottom;
        }

        // ---------- the pen: one pass of a silhouette is drawn in one ink, shifted by the pass's offset ----------

        private static Color _ink;
        private static Color _hole;
        private static float _dx;
        private static float _dy;

        private static void Begin(Color ink, Color hole, float dx, float dy)
        {
            _ink = ink;
            _hole = hole;
            _dx = dx;
            _dy = dy;
        }

        private static Vector2 At(float x, float y) => new((x + _dx) * SIZE / GRID, (y + _dy) * SIZE / GRID);

        private static float Scaled(float value) => value * SIZE / GRID;

        /// <summary>A filled shape through the points (x, y, x, y…).</summary>
        private static void Shape(Painter2D p, params float[] xy) => Shape(p, _ink, xy);

        /// <summary>The same in the colour of the tile: a cut in the silhouette.</summary>
        private static void Cut(Painter2D p, params float[] xy) => Shape(p, _hole, xy);

        private static void Shape(Painter2D p, Color color, float[] xy)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(At(xy[0], xy[1]));
            for (int i = 2; i + 1 < xy.Length; i += 2)
                p.LineTo(At(xy[i], xy[i + 1]));
            p.ClosePath();
            p.Fill();
        }

        private static void Dot(Painter2D p, float x, float y, float radius, bool cut = false)
        {
            p.fillColor = cut ? _hole : _ink;
            p.BeginPath();
            p.Arc(At(x, y), Scaled(radius), 0f, 360f);
            p.Fill();
        }

        // A thick curve, clockwise on the screen: 0° — to the right, 90° — down
        private static void Curve(Painter2D p, float x, float y, float radius, float from, float to, float width)
        {
            p.strokeColor = _ink;
            p.lineWidth = Scaled(width);
            p.BeginPath();
            p.Arc(At(x, y), Scaled(radius), from, to);
            p.Stroke();
        }

        private static void Stroke(Painter2D p, float width, params float[] xy)
        {
            p.strokeColor = _ink;
            p.lineWidth = Scaled(width);
            p.BeginPath();
            p.MoveTo(At(xy[0], xy[1]));
            for (int i = 2; i + 1 < xy.Length; i += 2)
                p.LineTo(At(xy[i], xy[i + 1]));
            p.Stroke();
        }

        // ---------- silhouettes (a 24 × 24 grid, the y axis down) ----------

        // A sword, point up and to the right: the blade, the guard, the grip, the pommel
        private static void Sword(Painter2D p)
        {
            Shape(p, 18.5f, 3f, 21f, 3f, 21f, 5.5f, 10.5f, 16f, 8f, 13.5f);
            Shape(p, 6f, 11f, 13f, 18f, 11.5f, 19.5f, 4.5f, 12.5f);
            Shape(p, 7.5f, 15f, 9f, 16.5f, 5.5f, 20f, 4f, 18.5f);
            Dot(p, 4f, 20f, 1.7f);
        }

        // A fight: two crossed swords, points up
        private static void CrossedSwords(Painter2D p)
        {
            Stroke(p, 2.6f, 20f, 3.5f, 6f, 17.5f);
            Stroke(p, 2.6f, 4f, 3.5f, 18f, 17.5f);
            Stroke(p, 2.2f, 4.5f, 13.5f, 10f, 19f);
            Stroke(p, 2.2f, 19.5f, 13.5f, 14f, 19f);
            Dot(p, 4.2f, 19.8f, 1.6f);
            Dot(p, 19.8f, 19.8f, 1.6f);
        }

        // An ability used: a spark with four points
        private static void Spark(Painter2D p)
        {
            Shape(p, 12f, 2.5f, 14.2f, 9.8f, 21.5f, 12f, 14.2f, 14.2f, 12f, 21.5f, 9.8f, 14.2f, 2.5f, 12f, 9.8f, 9.8f);
        }

        // A drawn bow with an arrow on the string
        private static void Bow(Painter2D p)
        {
            Curve(p, 5f, 12f, 9.5f, -58f, 58f, 2.8f);
            Stroke(p, 1.1f, 10f, 4f, 10f, 20f);
            Shape(p, 4f, 11f, 16f, 11f, 16f, 13f, 4f, 13f);
            Shape(p, 15f, 7.5f, 22f, 12f, 15f, 16.5f);
        }

        // An arrow that runs into a mark
        private static void Arrow(Painter2D p)
        {
            Shape(p, 2.5f, 10.3f, 12f, 10.3f, 12f, 13.7f, 2.5f, 13.7f);
            Shape(p, 11f, 6f, 18.5f, 12f, 11f, 18f);
            Shape(p, 19.8f, 5.5f, 22f, 5.5f, 22f, 18.5f, 19.8f, 18.5f);
        }

        // Fast after something
        private static void Chevrons(Painter2D p)
        {
            Shape(p, 3f, 5f, 7f, 5f, 12f, 12f, 7f, 19f, 3f, 19f, 8f, 12f);
            Shape(p, 11.5f, 5f, 15.5f, 5f, 20.5f, 12f, 15.5f, 19f, 11.5f, 19f, 16.5f, 12f);
        }

        // There and back
        private static void BackAndForth(Painter2D p)
        {
            Shape(p, 3f, 6.6f, 13f, 6.6f, 13f, 9.4f, 3f, 9.4f);
            Shape(p, 12f, 3.5f, 19.5f, 8f, 12f, 12.5f);
            Shape(p, 11f, 14.6f, 21f, 14.6f, 21f, 17.4f, 11f, 17.4f);
            Shape(p, 12f, 11.5f, 4.5f, 16f, 12f, 20.5f);
        }

        // Round a target to its far side
        private static void Around(Painter2D p)
        {
            Dot(p, 11.5f, 14.5f, 2.8f);
            Curve(p, 11.5f, 14.5f, 7.5f, 180f, 352f, 3f);
            Shape(p, 15f, 12.5f, 23f, 12.5f, 19f, 19f);
        }

        // Circling a target
        private static void Orbit(Painter2D p)
        {
            Dot(p, 12f, 12f, 2.8f);
            Curve(p, 12f, 12f, 8f, 200f, 312f, 2.8f);
            Shape(p, 20.3f, 4.8f, 15.9f, 8.6f, 20.6f, 10f);
            Curve(p, 12f, 12f, 8f, 20f, 132f, 2.8f);
            Shape(p, 3.7f, 19.2f, 8.1f, 15.4f, 3.4f, 14f);
        }

        // Keeping the distance: an arrow with two heads
        private static void Distance(Painter2D p)
        {
            Shape(p, 8f, 10.4f, 16f, 10.4f, 16f, 13.6f, 8f, 13.6f);
            Shape(p, 9.5f, 6f, 2.5f, 12f, 9.5f, 18f);
            Shape(p, 14.5f, 6f, 21.5f, 12f, 14.5f, 18f);
        }

        // A place taken
        private static void Tick(Painter2D p)
        {
            Curve(p, 12f, 12f, 8.3f, 0f, 360f, 2.4f);
            Stroke(p, 3f, 7.8f, 12.3f, 10.9f, 15.4f, 16.4f, 8.9f);
        }

        // A place given back
        private static void Minus(Painter2D p)
        {
            Curve(p, 12f, 12f, 8.3f, 0f, 360f, 2.4f);
            Shape(p, 7.5f, 10.5f, 16.5f, 10.5f, 16.5f, 13.5f, 7.5f, 13.5f);
        }

        // A shout
        private static void Megaphone(Painter2D p)
        {
            Shape(p, 2.5f, 9.3f, 7.5f, 9.3f, 14.5f, 4.5f, 14.5f, 19.5f, 7.5f, 14.7f, 2.5f, 14.7f);
            Curve(p, 14.5f, 12f, 3.8f, -42f, 42f, 2f);
            Curve(p, 14.5f, 12f, 7.4f, -42f, 42f, 2f);
        }

        // Work: a hammer, head up
        private static void Hammer(Painter2D p)
        {
            Shape(p, 5f, 21f, 3f, 19f, 11.5f, 10.5f, 13.5f, 12.5f);
            Shape(p, 12f, 4f, 20f, 12f, 17f, 15f, 9f, 7f);
        }

        // A talk: two speech bubbles
        private static void Talk(Painter2D p)
        {
            Shape(p, 3f, 3f, 14f, 3f, 14f, 10f, 8f, 10f, 5.5f, 13f, 5.5f, 10f, 3f, 10f);
            Shape(p, 10f, 12f, 21f, 12f, 21f, 18.5f, 18.5f, 18.5f, 18.5f, 21.5f, 15.5f, 18.5f, 10f, 18.5f);
        }

        private static void Hourglass(Painter2D p)
        {
            Shape(p, 6f, 3f, 18f, 3f, 18f, 5f, 13.5f, 12f, 18f, 19f, 18f, 21f, 6f, 21f, 6f, 19f, 10.5f, 12f, 6f, 5f);
        }

        // Something said: a speech bubble with two lines in it
        private static void Bubble(Painter2D p)
        {
            Shape(p, 3f, 4f, 21f, 4f, 21f, 15.5f, 12.5f, 15.5f, 7.5f, 20.5f, 7.5f, 15.5f, 3f, 15.5f);
            Cut(p, 6.5f, 7.4f, 17.5f, 7.4f, 17.5f, 9.2f, 6.5f, 9.2f);
            Cut(p, 6.5f, 11f, 14f, 11f, 14f, 12.8f, 6.5f, 12.8f);
        }

        private static void Eye(Painter2D p)
        {
            p.fillColor = _ink;
            p.BeginPath();
            p.MoveTo(At(2f, 12f));
            p.QuadraticCurveTo(At(12f, 3f), At(22f, 12f));
            p.QuadraticCurveTo(At(12f, 21f), At(2f, 12f));
            p.ClosePath();
            p.Fill();
            Dot(p, 12f, 12f, 3.5f, cut: true);
            Dot(p, 12f, 12f, 1.5f);
        }
    }
}
