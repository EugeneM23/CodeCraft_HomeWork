using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Loading;

namespace Game
{
    /// <summary>
    /// Compiles the game's code ahead, behind the loading screen, so the first crush, death or blast of a session does
    /// not pay the JIT in the middle of a fight (59–73 ms on the first Regax death in the editor). Every non-generic,
    /// non-abstract method of the given assemblies is compiled, and so are the methods of the closed generic types their
    /// fields, signatures and base types use (ReactiveVariable&lt;Vector2&gt;, Event&lt;TakeDamageArgs&gt;,
    /// List&lt;VoxelBody&gt;), a slice per tick within a frame budget. Compiling is asking Mono for the method's native
    /// code (RuntimeMethodHandle.GetFunctionPointer): Unity's Mono makes RuntimeHelpers.PrepareMethod a no-op. Nothing
    /// runs: no static constructor, no side effect. An assembly is compiled once a session — the next level skips it.
    /// Under IL2CPP the code is native already and the operation finishes at once. The IL of every method is read for
    /// the generic code it calls but does not declare: closed generic methods (entity.GetValue&lt;T&gt;) and the closed
    /// generic types it makes (new Dictionary&lt;string, Transform&gt;()).
    /// </summary>
    public sealed class WarmUpCodeOperation : LoadingOperation
    {
        private const BindingFlags ALL_DECLARED = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                                  BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Once a session; cleared with the domain (a Play without domain reload keeps the code compiled)
        private static readonly HashSet<Assembly> s_warmedAssemblies = new();
        private static readonly HashSet<Type> s_warmedTypes = new();

        private readonly Assembly[] _assemblies;
        private readonly double _frameBudgetMs;
        private readonly List<RuntimeMethodHandle> _methods = new();
        private int _next;

        /// <param name="frameBudgetMs">Compile time spent per tick; the loading screen keeps its frame rate.</param>
        public WarmUpCodeOperation(IReadOnlyList<Assembly> assemblies, double frameBudgetMs = 8d, float weight = 1f)
            : base(weight)
        {
            _assemblies = assemblies != null ? new List<Assembly>(assemblies).ToArray() : Array.Empty<Assembly>();
            _frameBudgetMs = frameBudgetMs > 0d ? frameBudgetMs : 1d;
        }

        /// <summary>Methods compiled by this run (0 when everything was warm already or under IL2CPP).</summary>
        public int Compiled { get; private set; }

        /// <summary>Methods this run has to compile.</summary>
        public int Total => _methods.Count;

        protected override void OnBegin()
        {
            _methods.Clear();
            _next = 0;
            Compiled = 0;

#if ENABLE_IL2CPP
            Succeed();
#else
            var generics = new List<Type>();
            foreach (Assembly assembly in _assemblies)
            {
                if (assembly == null || !s_warmedAssemblies.Add(assembly))
                    continue;

                foreach (Type type in TypesOf(assembly))
                {
                    if (type == null || type.ContainsGenericParameters || type.IsInterface)
                        continue;

                    AddMethods(type, _methods);
                    CollectClosedGenerics(type, generics);
                    ScanBodies(type, generics, _methods);
                }
            }

            // The list grows while it is read: a closed generic type of our own code (Atomic's Event<T>) is read for
            // the generic code it calls in turn; the runtime's own (List<T>) is only compiled, not followed
            var own = new HashSet<Assembly>(_assemblies);
            for (int i = 0; i < generics.Count; i++)
            {
                Type generic = generics[i];
                AddMethods(generic, _methods);
                if (own.Contains(generic.GetGenericTypeDefinition().Assembly))
                    ScanBodies(generic, generics, _methods);
            }

            if (_methods.Count == 0)
                Succeed();
#endif
        }

        protected override void OnTick(float dt)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (_next < _methods.Count)
            {
                Compile(_methods[_next++]);
                Compiled++;
                if (clock.Elapsed.TotalMilliseconds >= _frameBudgetMs)
                    return;
            }

            Succeed();
        }

        protected override float MeasureProgress() => _methods.Count == 0 ? 0f : (float)_next / _methods.Count;

        private static Type[] TypesOf(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }

        private static void AddMethods(Type type, List<RuntimeMethodHandle> methods)
        {
            foreach (MethodInfo method in type.GetMethods(ALL_DECLARED))
            {
                if (CanCompile(method))
                    methods.Add(method.MethodHandle);
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(ALL_DECLARED))
            {
                // Static constructors run when they would: compiling one is not running it, but leave them be
                if (!constructor.IsStatic && CanCompile(constructor))
                    methods.Add(constructor.MethodHandle);
            }
        }

        // The constructed generic types the type works with: its base types, fields and method signatures
        private static void CollectClosedGenerics(Type type, List<Type> generics)
        {
            for (Type parent = type.BaseType; parent != null; parent = parent.BaseType)
                AddClosedGeneric(parent, generics);

            foreach (FieldInfo field in type.GetFields(ALL_DECLARED))
                AddClosedGeneric(field.FieldType, generics);

            foreach (MethodInfo method in type.GetMethods(ALL_DECLARED))
            {
                if (method.ContainsGenericParameters)
                    continue;

                AddClosedGeneric(method.ReturnType, generics);
                foreach (ParameterInfo parameter in method.GetParameters())
                    AddClosedGeneric(parameter.ParameterType, generics);
            }
        }

        private static void AddClosedGeneric(Type type, List<Type> generics)
        {
            while (type != null && (type.IsArray || type.IsByRef || type.IsPointer))
                type = type.GetElementType();

            if (type == null || !type.IsGenericType || type.ContainsGenericParameters || !s_warmedTypes.Add(type))
                return;

            generics.Add(type);
            foreach (Type argument in type.GetGenericArguments())
                AddClosedGeneric(argument, generics);
        }

        // ---------- Reading the IL for the generic code a method uses ----------

        private static readonly Dictionary<short, OpCode> s_opCodes = OpCodesByValue();
        private static readonly HashSet<MethodBase> s_warmedGenericMethods = new();

        private static Dictionary<short, OpCode> OpCodesByValue()
        {
            var opCodes = new Dictionary<short, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is OpCode opCode)
                    opCodes[opCode.Value] = opCode;
            }

            return opCodes;
        }

        private static void ScanBodies(Type type, List<Type> generics, List<RuntimeMethodHandle> methods)
        {
            Type[] typeArguments = type.IsGenericType ? type.GetGenericArguments() : null;
            foreach (MethodInfo method in type.GetMethods(ALL_DECLARED))
            {
                if (CanCompile(method))
                    ScanBody(method, typeArguments, generics, methods);
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(ALL_DECLARED))
            {
                if (CanCompile(constructor))
                    ScanBody(constructor, typeArguments, generics, methods);
            }
        }

        private static void ScanBody(MethodBase method, Type[] typeArguments, List<Type> generics,
            List<RuntimeMethodHandle> methods)
        {
            byte[] il;
            try
            {
                il = method.GetMethodBody()?.GetILAsByteArray();
            }
            catch (Exception)
            {
                return;
            }

            if (il == null)
                return;

            Module module = method.Module;
            int i = 0;
            while (i < il.Length)
            {
                short value = il[i] == 0xFE && i + 1 < il.Length ? (short)(0xFE00 | il[i + 1]) : il[i];
                if (!s_opCodes.TryGetValue(value, out OpCode opCode))
                    return; // an unknown byte: stop reading this body rather than misread it

                i += opCode.Size;
                if (opCode.OperandType == OperandType.InlineMethod && i + 4 <= il.Length)
                    AddCalled(module, BitConverter.ToInt32(il, i), typeArguments, generics, methods);

                i += OperandSize(opCode.OperandType, il, i);
            }
        }

        private static void AddCalled(Module module, int token, Type[] typeArguments, List<Type> generics,
            List<RuntimeMethodHandle> methods)
        {
            MethodBase called;
            try
            {
                called = module.ResolveMethod(token, typeArguments, null);
            }
            catch (Exception)
            {
                return;
            }

            if (called == null || called.ContainsGenericParameters)
                return;

            // new List<VoxelBody>(), Dictionary<string, Transform>.TryAdd: the whole closed type comes with it
            AddClosedGeneric(called.DeclaringType, generics);

            // entity.GetValue<Health>(key): the instantiation itself
            if (called.IsGenericMethod && CanCompileInstance(called) && s_warmedGenericMethods.Add(called))
                methods.Add(called.MethodHandle);
        }

        private static bool CanCompileInstance(MethodBase method)
        {
            if (method.IsAbstract)
                return false;

            MethodImplAttributes flags = method.GetMethodImplementationFlags();
            return (flags & (MethodImplAttributes.InternalCall | MethodImplAttributes.Native |
                             MethodImplAttributes.Runtime)) == 0;
        }

        private static int OperandSize(OperandType operand, byte[] il, int at)
        {
            switch (operand)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return at + 4 <= il.Length ? 4 + 4 * BitConverter.ToInt32(il, at) : il.Length;
                default:
                    return 4;
            }
        }

        private static bool CanCompile(MethodBase method)
        {
            if (method.IsAbstract || method.ContainsGenericParameters)
                return false;

            MethodImplAttributes flags = method.GetMethodImplementationFlags();
            return (flags & (MethodImplAttributes.InternalCall | MethodImplAttributes.Native |
                             MethodImplAttributes.Runtime)) == 0;
        }

        private static void Compile(RuntimeMethodHandle method)
        {
            try
            {
                method.GetFunctionPointer();
            }
            catch (Exception)
            {
                // A method the runtime will not compile ahead (an extern, a broken reference) is compiled on its
                // first call as before
            }
        }
    }
}
