// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using System.Text.Json;

namespace Amazon.Lambda.RuntimeSupport.Helpers.Logging
{
    /// <summary>
    /// Bridges the structured logging configuration callback in Amazon.Lambda.RuntimeSupport with the
    /// <c>Amazon.Lambda.Core.LambdaLogger.ConfigureStructuredLogging</c> API in Amazon.Lambda.Core.
    ///
    /// There are two very different ways Amazon.Lambda.RuntimeSupport gets its reference to Amazon.Lambda.Core:
    ///   * Executable / custom runtime model: the customer references both Amazon.Lambda.Core and
    ///     Amazon.Lambda.RuntimeSupport from the same deployment bundle, so the compile-time reference
    ///     to <c>Amazon.Lambda.Core.LambdaLogger</c> resolves to the exact same assembly the customer code uses.
    ///   * Class library model on the managed runtime: Amazon.Lambda.RuntimeSupport is baked into the managed
    ///     runtime and compiled against its own (potentially older) copy of Amazon.Lambda.Core, while the customer's
    ///     Amazon.Lambda.Core is loaded separately from the deployment bundle by <see cref="Bootstrap.UserCodeLoader"/>.
    ///     In that case the compile-time reference used by <see cref="ConfigureCallbackInCore(Action{StructuredLoggingOptions})"/>
    ///     points at the WRONG LambdaLogger, so the customer's call to <c>LambdaLogger.ConfigureStructuredLogging</c>
    ///     never reaches the formatter. See https://github.com/aws/aws-lambda-dotnet/issues/2350.
    ///
    /// To support the class library model this class can also wire the callback into a specific, reflection-loaded
    /// Amazon.Lambda.Core assembly via <see cref="ConfigureCallbackInCore(Assembly, Action{StructuredLoggingOptions})"/>.
    /// </summary>
    internal class ConfigureJsonLogMessageFormatterIsolated
    {
        // Field and method names on Amazon.Lambda.Core.LambdaLogger that we bind to reflectively. These MUST match
        // the members declared in Amazon.Lambda.Core.LambdaLogger. They are only used for the reflection based path
        // (class library model) where the compile-time reference cannot be relied upon.
        private const string LambdaLoggerTypeName = "Amazon.Lambda.Core.LambdaLogger";
        private const string StructuredLoggingOptionsTypeName = "Amazon.Lambda.Core.StructuredLoggingOptions";
        private const string SetConfigureStructuredLoggingActionMethodName = "SetConfigureStructuredLoggingAction";
        private const string OverrideSerializerOptionsPropertyName = "OverrideSerializerOptions";

        /// <summary>
        /// Wire the callback into the version of Amazon.Lambda.Core referenced at compile time. This is the correct
        /// assembly for the executable / custom runtime programming model.
        /// </summary>
        /// <param name="callback">Callback invoked with the customer supplied structured logging options.</param>
        internal static void ConfigureCallbackInCore(Action<StructuredLoggingOptions> callback)
        {
            Amazon.Lambda.Core.LambdaLogger.SetConfigureStructuredLoggingAction((Amazon.Lambda.Core.StructuredLoggingOptions coreOptions) =>
            {
                if (coreOptions == null)
                {
                    callback(null);
                    return;
                }

                var isolatedOptions = new StructuredLoggingOptions();
                try
                {
                    isolatedOptions.OverrideSerializerOptions = coreOptions.OverrideSerializerOptions;
                }
                catch (Exception ex)
                {
                    InternalLogger.GetDefaultLogger().LogDebug("Failed to configure structured logging. This generally happens when the version of Amazon.Lambda.Core is out of date. Update to latest version of Amazon.Lambda.Core: " + ex.ToString());
                }

                callback(isolatedOptions);
            });
        }

        /// <summary>
        /// Wire the callback into a specific, reflection-loaded copy of Amazon.Lambda.Core. This is required for the
        /// class library programming model on the managed runtime, where the customer's Amazon.Lambda.Core is a
        /// different assembly than the one Amazon.Lambda.RuntimeSupport was compiled against.
        ///
        /// The whole call is done through reflection because we cannot cast the callback (which uses the RuntimeSupport
        /// bundled types) to the delegate type expected by the customer's Amazon.Lambda.Core. Instead we build a
        /// weakly typed <see cref="Action{T}"/> where T is the customer's StructuredLoggingOptions type and read the
        /// <c>OverrideSerializerOptions</c> property off the supplied instance reflectively.
        /// </summary>
        /// <param name="coreAssembly">The customer's Amazon.Lambda.Core assembly loaded by the UserCodeLoader.</param>
        /// <param name="callback">Callback invoked with the customer supplied structured logging options.</param>
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Uses reflection against the customer's Amazon.Lambda.Core. Only used in the class library programming model which does not support trimming.")]
        internal static void ConfigureCallbackInCore(Assembly coreAssembly, Action<StructuredLoggingOptions> callback)
        {
            if (coreAssembly == null)
            {
                throw new ArgumentNullException(nameof(coreAssembly));
            }

            var logger = InternalLogger.GetDefaultLogger();

            var lambdaLoggerType = coreAssembly.GetType(LambdaLoggerTypeName);
            if (lambdaLoggerType == null)
            {
                logger.LogDebug($"Structured logging callback not configured: could not find type {LambdaLoggerTypeName} in the customer's Amazon.Lambda.Core.");
                return;
            }

            var optionsType = coreAssembly.GetType(StructuredLoggingOptionsTypeName);
            if (optionsType == null)
            {
                // This happens when the customer references a version of Amazon.Lambda.Core that predates the
                // ConfigureStructuredLogging API. Nothing to wire up in that case.
                logger.LogDebug($"Structured logging callback not configured: the customer's Amazon.Lambda.Core does not contain {StructuredLoggingOptionsTypeName}. Update Amazon.Lambda.Core to use structured logging customization.");
                return;
            }

            // SetConfigureStructuredLoggingAction(Action<StructuredLoggingOptions>) is internal on LambdaLogger.
            var setActionMethod = lambdaLoggerType.GetMethod(
                SetConfigureStructuredLoggingActionMethodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            if (setActionMethod == null)
            {
                logger.LogDebug($"Structured logging callback not configured: could not find method {SetConfigureStructuredLoggingActionMethodName} on {LambdaLoggerTypeName}. Update Amazon.Lambda.Core to use structured logging customization.");
                return;
            }

            var overrideSerializerOptionsProperty = optionsType.GetProperty(
                OverrideSerializerOptionsPropertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (overrideSerializerOptionsProperty == null)
            {
                logger.LogDebug($"Structured logging callback not configured: could not find property {OverrideSerializerOptionsPropertyName} on {StructuredLoggingOptionsTypeName}.");
                return;
            }

            // Build an Action<customerStructuredLoggingOptionsType> that reads OverrideSerializerOptions reflectively
            // and forwards a RuntimeSupport-typed StructuredLoggingOptions to the callback.
            //
            // We use a non-generic delegate wrapper (Action<object>) and adapt it to the strongly typed delegate the
            // internal method expects with Delegate.CreateDelegate via a helper method that has the right signature.
            var forwarder = new StructuredLoggingCallbackForwarder(callback, overrideSerializerOptionsProperty);

            // The internal method's parameter is Action<TCustomerOptions>. Construct that delegate type and bind it to
            // the forwarder's Invoke method which accepts object (compatible because reference types are covariant here
            // only through an explicit adapter). Because Action<T> is not covariant, we instead create the delegate from
            // the forwarder instance method that takes object, using a dynamically constructed Action<TCustomerOptions>.
            var actionOfOptionsType = typeof(Action<>).MakeGenericType(optionsType);
            Delegate stronglyTypedDelegate;
            try
            {
                stronglyTypedDelegate = Delegate.CreateDelegate(
                    actionOfOptionsType,
                    forwarder,
                    nameof(StructuredLoggingCallbackForwarder.Invoke));
            }
            catch (Exception ex)
            {
                logger.LogDebug("Structured logging callback not configured: failed to bind delegate to the customer's Amazon.Lambda.Core StructuredLoggingOptions type: " + ex);
                return;
            }

            try
            {
                setActionMethod.Invoke(null, new object[] { stronglyTypedDelegate });
                logger.LogDebug("Structured logging callback configured against the customer's Amazon.Lambda.Core using reflection.");
            }
            catch (Exception ex)
            {
                logger.LogDebug("Structured logging callback not configured: failed to invoke SetConfigureStructuredLoggingAction on the customer's Amazon.Lambda.Core: " + ex);
            }
        }

        /// <summary>
        /// Adapter that receives the customer's StructuredLoggingOptions instance (typed as object so it works across
        /// assembly boundaries) and forwards the relevant values to the RuntimeSupport structured logging callback.
        /// The <see cref="Invoke"/> method is bound to an <c>Action&lt;TCustomerOptions&gt;</c> via
        /// <see cref="Delegate.CreateDelegate(Type, object, string)"/>; the parameter is declared as <see cref="object"/>
        /// because reference-type contravariance allows the customer options instance to be passed to it.
        /// </summary>
        private sealed class StructuredLoggingCallbackForwarder
        {
            private readonly Action<StructuredLoggingOptions> _callback;
            private readonly PropertyInfo _overrideSerializerOptionsProperty;

            public StructuredLoggingCallbackForwarder(Action<StructuredLoggingOptions> callback, PropertyInfo overrideSerializerOptionsProperty)
            {
                _callback = callback;
                _overrideSerializerOptionsProperty = overrideSerializerOptionsProperty;
            }

            // Bound to Action<TCustomerOptions>. TCustomerOptions is a reference type, so it is assignment compatible
            // with object which lets CreateDelegate bind this method to the strongly typed delegate.
            public void Invoke(object coreOptions)
            {
                if (coreOptions == null)
                {
                    _callback(null);
                    return;
                }

                var isolatedOptions = new StructuredLoggingOptions();
                try
                {
                    isolatedOptions.OverrideSerializerOptions = _overrideSerializerOptionsProperty.GetValue(coreOptions) as JsonSerializerOptions;
                }
                catch (Exception ex)
                {
                    InternalLogger.GetDefaultLogger().LogDebug("Failed to read structured logging options from the customer's Amazon.Lambda.Core. This generally happens when the version of Amazon.Lambda.Core is out of date. Update to latest version of Amazon.Lambda.Core: " + ex);
                }

                _callback(isolatedOptions);
            }
        }
    }
}
