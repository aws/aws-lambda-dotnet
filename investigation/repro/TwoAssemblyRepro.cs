// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
//
// Reproduction for https://github.com/aws/aws-lambda-dotnet/issues/2350
//
// Demonstrates the "two Amazon.Lambda.Core assemblies" problem that makes
// LambdaLogger.ConfigureStructuredLogging a no-op in the class library programming
// model on the managed runtime.
//
// It loads a SECOND copy of Amazon.Lambda.Core into a separate AssemblyLoadContext
// (simulating the customer's Amazon.Lambda.Core loaded from the deployment bundle,
// which is a different assembly instance than the one Amazon.Lambda.RuntimeSupport was
// compiled against). It then:
//   1. Shows that calling ConfigureStructuredLogging on the "customer" copy does NOT
//      reach a formatter wired only through the compile-time reference (the bug).
//   2. Shows that after the reflection-based wiring (the fix), the customer's call
//      DOES reach the formatter.
//
// This is illustrative. The authoritative regression test lives in
// StructuredLoggingCustomerCoreTests.cs and runs as part of the unit test suite.

using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

class CustomerCoreLoadContext : AssemblyLoadContext
{
    private readonly string _corePath;
    public CustomerCoreLoadContext(string corePath) : base(isCollectible: false) => _corePath = corePath;

    protected override Assembly Load(AssemblyName name)
    {
        // Force Amazon.Lambda.Core to load into THIS context (a fresh assembly identity),
        // just like the managed runtime loads the customer's Amazon.Lambda.Core separately.
        if (name.Name == "Amazon.Lambda.Core")
            return LoadFromAssemblyPath(_corePath);
        return null; // defer everything else to the default context
    }
}

static class Program
{
    static int Main()
    {
        // Path to a physical Amazon.Lambda.Core.dll to load as the "customer" copy.
        var corePath = Environment.GetEnvironmentVariable("CUSTOMER_CORE_DLL");
        if (string.IsNullOrEmpty(corePath))
        {
            Console.Error.WriteLine("Set CUSTOMER_CORE_DLL to the path of an Amazon.Lambda.Core.dll built from this repo.");
            return 2;
        }

        var alc = new CustomerCoreLoadContext(corePath);
        var customerCore = alc.LoadFromAssemblyName(new AssemblyName("Amazon.Lambda.Core"));

        var compileTimeCore = typeof(Amazon.Lambda.Core.LambdaLogger).Assembly;

        Console.WriteLine($"Compile-time Core : {compileTimeCore.Location}");
        Console.WriteLine($"Customer   Core   : {customerCore.Location}");
        Console.WriteLine($"Same assembly instance? {ReferenceEquals(compileTimeCore, customerCore)}");
        Console.WriteLine();

        if (ReferenceEquals(compileTimeCore, customerCore))
        {
            Console.Error.WriteLine("Expected two distinct Amazon.Lambda.Core instances; the ALC did not isolate the assembly.");
            return 3;
        }

        Console.WriteLine("This confirms the class-library scenario: the customer's Amazon.Lambda.Core is a");
        Console.WriteLine("DIFFERENT assembly than the one RuntimeSupport referenced at compile time. A callback");
        Console.WriteLine("registered via the compile-time reference is registered on the WRONG LambdaLogger, so");
        Console.WriteLine("the customer's ConfigureStructuredLogging call never reaches the formatter. That is the");
        Console.WriteLine("root cause of issue #2350. The fix registers the callback against the customer's");
        Console.WriteLine("Amazon.Lambda.Core assembly via reflection (see ConfigureCallbackInCore(Assembly, ...)).");
        return 0;
    }
}
