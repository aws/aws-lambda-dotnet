// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Amazon.Lambda.DurableExecution.Analyzers
{
    /// <summary>
    /// How a delegate passed to a durable operation executes, which determines what the
    /// analyzers allow inside its body.
    /// </summary>
    internal enum DurableDelegateRole
    {
        /// <summary>Not a delegate accepted by a durable operation.</summary>
        None,

        /// <summary>
        /// The delegate body runs inside a checkpointed step (StepAsync func,
        /// WaitForCallbackAsync submitter, WaitForConditionAsync check). Non-deterministic code is
        /// allowed here because the result is checkpointed; nested durable operations are not.
        /// </summary>
        StepWrapped,

        /// <summary>
        /// The delegate body runs as a child sub-workflow (RunInChildContextAsync func, ParallelAsync
        /// branch, MapAsync func). It is still workflow code (must be deterministic), but nested
        /// durable operations are allowed.
        /// </summary>
        ChildContext,
    }

    /// <summary>
    /// Per-compilation cache of the durable-execution and BCL symbols the analyzers match against.
    /// Resolved once in a compilation-start action; if <c>IDurableContext</c> is not present
    /// the compilation does not use durable execution and the analyzers register nothing.
    /// </summary>
    internal sealed class DurableKnownSymbols
    {
        internal const string IDurableContextMetadataName = "Amazon.Lambda.DurableExecution.IDurableContext";

        /// <summary>The names of the durable operations declared on <c>IDurableContext</c>.</summary>
        private static readonly ImmutableHashSet<string> DurableOperationNames = ImmutableHashSet.Create(
            "StepAsync",
            "WaitAsync",
            "RunInChildContextAsync",
            "CreateCallbackAsync",
            "WaitForCallbackAsync",
            "InvokeAsync",
            "WaitForConditionAsync",
            "ParallelAsync",
            "MapAsync");

        internal INamedTypeSymbol DurableContext { get; }

        // BCL types backing the DE001 non-determinism catalog. Any may be null on a given target framework.
        private readonly INamedTypeSymbol? _dateTime;
        private readonly INamedTypeSymbol? _dateTimeOffset;
        private readonly INamedTypeSymbol? _guid;
        private readonly INamedTypeSymbol? _random;
        private readonly INamedTypeSymbol? _stopwatch;
        private readonly INamedTypeSymbol? _environment;
        private readonly INamedTypeSymbol? _path;
        private readonly INamedTypeSymbol? _randomNumberGenerator;
        private readonly INamedTypeSymbol? _rngCryptoServiceProvider;
        private readonly INamedTypeSymbol? _task;
        private readonly INamedTypeSymbol? _taskOfT;

        private DurableKnownSymbols(Compilation compilation, INamedTypeSymbol durableContext)
        {
            DurableContext = durableContext;
            _dateTime = compilation.GetTypeByMetadataName("System.DateTime");
            _dateTimeOffset = compilation.GetTypeByMetadataName("System.DateTimeOffset");
            _guid = compilation.GetTypeByMetadataName("System.Guid");
            _random = compilation.GetTypeByMetadataName("System.Random");
            _stopwatch = compilation.GetTypeByMetadataName("System.Diagnostics.Stopwatch");
            _environment = compilation.GetTypeByMetadataName("System.Environment");
            _path = compilation.GetTypeByMetadataName("System.IO.Path");
            _randomNumberGenerator = compilation.GetTypeByMetadataName("System.Security.Cryptography.RandomNumberGenerator");
            _rngCryptoServiceProvider = compilation.GetTypeByMetadataName("System.Security.Cryptography.RNGCryptoServiceProvider");
            _task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            _taskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        }

        /// <summary>
        /// Resolves the durable symbols. Returns <c>null</c> when the compilation does not reference
        /// <c>Amazon.Lambda.DurableExecution</c>, so callers register no per-node work.
        /// </summary>
        internal static DurableKnownSymbols? TryCreate(Compilation compilation)
        {
            var durableContext = compilation.GetTypeByMetadataName(IDurableContextMetadataName);
            return durableContext is null ? null : new DurableKnownSymbols(compilation, durableContext);
        }

        /// <summary>
        /// True if <paramref name="type"/> is <c>IDurableContext</c> or implements it (covers the
        /// concrete <c>DurableContext</c> and any user implementation).
        /// </summary>
        internal bool IsDurableContextType(ITypeSymbol? type)
        {
            if (type is null)
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, DurableContext))
            {
                return true;
            }

            foreach (var iface in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, DurableContext))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True if <paramref name="method"/> is one of the durable operations on a durable context.
        /// Matched by symbol (containing type is/implements <c>IDurableContext</c>) plus an explicit
        /// name allowlist, so property getters and <c>ConfigureLogger</c> are excluded, and an
        /// unrelated <c>obj.StepAsync()</c> on some other type is never matched.
        /// </summary>
        internal bool IsDurableOperation(IMethodSymbol? method, out string operationName, out DurableDelegateRole role)
        {
            operationName = string.Empty;
            role = DurableDelegateRole.None;

            if (method is null)
            {
                return false;
            }

            var name = method.OriginalDefinition.Name;
            if (!DurableOperationNames.Contains(name))
            {
                return false;
            }

            if (!IsDurableContextType(method.OriginalDefinition.ContainingType))
            {
                return false;
            }

            operationName = name;
            role = RoleFor(name);
            return true;
        }

        private static DurableDelegateRole RoleFor(string operationName)
        {
            switch (operationName)
            {
                case "StepAsync":
                case "WaitForCallbackAsync":
                case "WaitForConditionAsync":
                    return DurableDelegateRole.StepWrapped;
                case "RunInChildContextAsync":
                case "ParallelAsync":
                case "MapAsync":
                    return DurableDelegateRole.ChildContext;
                default:
                    return DurableDelegateRole.None;
            }
        }

        /// <summary>True if <paramref name="method"/> is <c>Task.WhenAll</c> or <c>Task.WhenAny</c>.</summary>
        internal bool IsTaskCombinator(IMethodSymbol? method, out string friendlyName)
        {
            friendlyName = string.Empty;
            if (method is null || _task is null)
            {
                return false;
            }

            var def = method.OriginalDefinition;
            if (!SymbolEqualityComparer.Default.Equals(def.ContainingType, _task))
            {
                return false;
            }

            if (def.Name == "WhenAll")
            {
                friendlyName = "Task.WhenAll";
                return true;
            }

            if (def.Name == "WhenAny")
            {
                friendlyName = "Task.WhenAny";
                return true;
            }

            return false;
        }

        /// <summary>True if the type is <c>Task</c> or <c>Task&lt;T&gt;</c> (a candidate durable task).</summary>
        internal bool IsTaskType(ITypeSymbol? type)
        {
            if (type is null)
            {
                return false;
            }

            var def = type.OriginalDefinition;
            return SymbolEqualityComparer.Default.Equals(def, _task)
                || SymbolEqualityComparer.Default.Equals(def, _taskOfT);
        }

        /// <summary>
        /// Returns the friendly name (e.g. <c>"DateTime.Now"</c>) if the operation reads/calls/creates
        /// a non-deterministic API from the DE001 catalog; otherwise <c>null</c>.
        /// </summary>
        internal string? TryGetNonDeterministicApi(IOperation operation)
        {
            switch (operation)
            {
                case IPropertyReferenceOperation pr:
                    return MatchProperty(pr.Property);
                case IInvocationOperation inv:
                    return MatchMethod(inv.TargetMethod);
                case IObjectCreationOperation oc:
                    return MatchObjectCreation(oc);
                default:
                    return null;
            }
        }

        private string? MatchProperty(IPropertySymbol property)
        {
            var owner = property.ContainingType?.OriginalDefinition;
            var name = property.Name;

            if (SymbolEqualityComparer.Default.Equals(owner, _dateTime)
                && (name == "Now" || name == "UtcNow" || name == "Today"))
            {
                return "DateTime." + name;
            }

            if (SymbolEqualityComparer.Default.Equals(owner, _dateTimeOffset)
                && (name == "Now" || name == "UtcNow"))
            {
                return "DateTimeOffset." + name;
            }

            if (SymbolEqualityComparer.Default.Equals(owner, _environment)
                && (name == "TickCount" || name == "TickCount64"))
            {
                return "Environment." + name;
            }

            if (SymbolEqualityComparer.Default.Equals(owner, _stopwatch)
                && (name == "Elapsed" || name == "ElapsedMilliseconds" || name == "ElapsedTicks"))
            {
                return "Stopwatch." + name;
            }

            // Random.Shared is seeded non-deterministically. (A user-seeded `new Random(42)` is
            // deterministic, so we flag the seedless ctor / Random.Shared, not the .Next() call.)
            if (SymbolEqualityComparer.Default.Equals(owner, _random) && name == "Shared")
            {
                return "Random.Shared";
            }

            return null;
        }

        private string? MatchMethod(IMethodSymbol method)
        {
            var owner = method.ContainingType?.OriginalDefinition;
            var name = method.Name;

            if (SymbolEqualityComparer.Default.Equals(owner, _guid) && name == "NewGuid")
            {
                return "Guid.NewGuid()";
            }

            // Note: Random instance methods (.Next() etc.) are intentionally NOT flagged here — a
            // user-seeded `new Random(42)` is deterministic. Non-determinism is introduced by the
            // seedless `new Random()` ctor (MatchObjectCreation) and `Random.Shared` (MatchProperty).
            if (SymbolEqualityComparer.Default.Equals(owner, _stopwatch)
                && (name == "GetTimestamp" || name == "StartNew"))
            {
                return "Stopwatch." + name + "()";
            }

            if (SymbolEqualityComparer.Default.Equals(owner, _randomNumberGenerator)
                && (name == "GetBytes" || name == "GetInt32" || name == "Fill"
                    || name == "GetString" || name == "GetHexString"))
            {
                return "RandomNumberGenerator." + name + "()";
            }

            if (SymbolEqualityComparer.Default.Equals(owner, _path)
                && (name == "GetTempFileName" || name == "GetRandomFileName"))
            {
                return "Path." + name + "()";
            }

            return null;
        }

        private string? MatchObjectCreation(IObjectCreationOperation oc)
        {
            var type = oc.Constructor?.ContainingType?.OriginalDefinition;

            // Only the seedless Random ctor is non-deterministic; new Random(seed) is fine.
            if (SymbolEqualityComparer.Default.Equals(type, _random) && oc.Arguments.Length == 0)
            {
                return "new Random()";
            }

            if (SymbolEqualityComparer.Default.Equals(type, _rngCryptoServiceProvider))
            {
                return "new RNGCryptoServiceProvider()";
            }

            return null;
        }

        /// <summary>
        /// True if <paramref name="parameters"/> contains an <c>IDurableContext</c>-typed parameter,
        /// which marks a method/local-function/lambda as durable workflow code.
        /// </summary>
        internal bool HasDurableContextParameter(IEnumerable<IParameterSymbol> parameters)
        {
            foreach (var p in parameters)
            {
                if (IsDurableContextType(p.Type))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
