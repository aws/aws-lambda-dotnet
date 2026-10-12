# Pester can only mock a command that RESOLVES: Mock looks the name up first and throws
# 'Could not find Command docker' when it cannot. The docker tests never run docker -- every call is
# mocked -- but on a host without the CLI installed the lookup itself fails, so the whole file errors
# out instead of running offline as intended. Declaring a global stub makes the name resolvable from
# both the test session and the module's session state (which is what 'Mock -ModuleName' searches),
# without shadowing a real docker when one is present.

function Add-LambdaPSDockerStub {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Test helper: it defines a no-op function so Mock can resolve the name.')]
    param()

    if (Get-Command -Name 'docker' -ErrorAction SilentlyContinue) { return }

    # Deliberately param-less: the mocks are asserted with -ParameterFilter on $args, so the stub
    # must take its arguments the way the real native command does.
    Set-Item -Path 'function:global:docker' -Value { }
}
