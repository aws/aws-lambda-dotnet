---
name: lambda-annotations
description: Conventions and safety rules for changing the Amazon.Lambda.Annotations library and its source generator, including the T4 templates that generate Lambda handler code. Use when modifying parameter binding, generated handler code, authorizer support, or the source generator templates.
---

# Working on Amazon.Lambda.Annotations

This skill covers rules for changing the Amazon.Lambda.Annotations source generator. The code it generates is compiled into customer assemblies. A bug in a template becomes a bug in every customer function built with that version, and a fix only reaches customers when they rebuild and redeploy.

## Key Locations

- **Attributes and runtime types**: `Libraries/src/Amazon.Lambda.Annotations/`
- **Source generator**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/`
- **Templates**: `Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Templates/`
  - `*.tt`: T4 templates (the source of truth)
  - `*.cs` with the same name as a `.tt`: preprocessed output from `TextTemplatingFilePreprocessor`. Keep it in sync with the `.tt`.
  - `*Code.cs`: hand-written partial classes for the templates. Put non-trivial generation logic here rather than in the `.tt` so the preprocessed `.cs` changes stay small.
- **Source generator tests**: `Libraries/test/Amazon.Lambda.Annotations.SourceGenerators.Tests/`
  - `Snapshots/`: expected generated code. Update these when template output changes.
  - `AuthorizerBindingFailureTests.cs`: runs the generator, compiles the output and invokes the generated handlers. Use it as the pattern for runtime behavior tests of generated code.

## Template Editing

- Edit the `.tt` and the matching preprocessed `.cs` together. Regenerating with Visual Studio is preferred. If you edit the `.cs` by hand, mirror the `.tt` exactly and keep the `this.Write(...)` strings using `\r\n` line endings like the rest of the file.
- Generated code that is emitted from `*Code.cs` helpers should also use `\r\n` line endings to match the T4 output.
- After changing templates, run the source generator tests on both target frameworks:
  ```
  cd Libraries/test/Amazon.Lambda.Annotations.SourceGenerators.Tests
  dotnet test
  ```
- Building the test projects rewrites the `serverless.template` files under `Libraries/test/*` with the current Annotations version. Revert those changes unless the template change is intentional.

## Parameter Binding Must Never Fail Silently

The templates convert client-supplied strings (headers, query string, route parameters, authorization tokens, authorizer context values) to the parameter type with `Convert.ChangeType`. **Every `Convert.ChangeType` failure must change the outcome of the request. Logging the failure and continuing is not acceptable.**

If a conversion failure is only logged, the parameter keeps its `default(T)` value (`0`, `false`, `MinValue`, `null`) and the user's method runs with a value the client never sent. For any security decision, an attacker can then choose that value by sending something that doesn't parse.

Required behavior by handler type:

| Handler type | Template | On conversion failure |
|---|---|---|
| API Gateway (`[RestApi]`, `[HttpApi]`) `[FromHeader]`, `[FromQuery]`, `[FromRoute]`, `[FromBody]` | `APIGatewaySetupParameters.tt` | Add to `validationErrors` and return **400** without invoking the user's method |
| API Gateway `[FromCustomAuthorizer]` | `APIGatewaySetupParameters.tt` | Return **401** without invoking the user's method |
| ALB | `ALBSetupParameters.tt` | Add to `validationErrors` and return **400** without invoking the user's method |
| Authorizers (`[HttpApiAuthorizer]`, `[RestApiAuthorizer]`) | `AuthorizerSetupParameters.tt` | **Deny** without invoking the user's authorizer method |

### Authorizers Must Fail Closed

An authorizer that runs on a default value can grant access. When binding fails, authorizer templates must deny the request:

- Each conversion `catch` block in `AuthorizerSetupParameters.tt` sets `__bindingFailed__ = true;` after logging.
- After all parameters are bound, the generated code checks `__bindingFailed__` and returns the deny response from `GenerateBindingFailureResponse()` in `AuthorizerSetupParametersCode.cs`. The deny response matches the authorizer's return type:
  - `IAuthorizerResult`: `AuthorizerResults.Deny()` serialized with the same format and method/route ARN as the normal path
  - `APIGatewayCustomAuthorizerV2SimpleResponse`: `IsAuthorized = false`
  - `APIGatewayCustomAuthorizerResponse` / `APIGatewayCustomAuthorizerV2IamResponse`: an explicit `Deny` policy
  - Any other type: `throw new Exception("Unauthorized")`
- The `__bindingFailed__` declaration and the deny check are both emitted only when `HasBoundParameters()` is true. If you add a new binding source, such as a new `[From*]` attribute or a new conversion branch, update `HasBoundParameters()`. If you forget, the generated code fails to compile because the flag is set but never declared. That's loud, but it fails in customer builds too.
- The dangerous mistake is a new conversion `catch` that doesn't set `__bindingFailed__`. That silently fails open and nothing catches it unless a test sends a malformed value.

### Checklist for Binding Changes

When adding or changing any code path that converts a client-supplied value:

1. Confirm the `catch` block changes the outcome of the request (validation error, 401 or deny) and doesn't only log.
2. For authorizers, confirm the `catch` sets `__bindingFailed__ = true;` and that `HasBoundParameters()` covers the new parameter source.
3. Quick audit of the authorizer template. The three counts should match:
   ```
   grep -c "Convert.ChangeType" Templates/AuthorizerSetupParameters.tt
   grep -c "catch (Exception e)" Templates/AuthorizerSetupParameters.tt
   grep -c "__bindingFailed__ = true;" Templates/AuthorizerSetupParameters.tt
   ```
4. Add a runtime test with a malformed value (for example `abc` and an overflowing number for a `long`). It should assert that the user's method is **not** invoked and that the response is a 400, 401 or deny as appropriate. Extend `AuthorizerBindingFailureTests.cs` for authorizers.
5. Remember that existing snapshot tests mostly use `string` parameters, which can't fail conversion. Snapshots alone don't prove the failure path works, so the runtime test in step 4 is required.

## Change Files

Every change needs an AutoVer change file (see `CONTRIBUTING.md`). Run from the repository root:
```
autover change --project-name "Amazon.Lambda.Annotations" -m "<changelog message>"
```
