# Builds a seekable payload stream, matching the SDK's MemoryStream Payload shape.
function New-PayloadStream {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Test helper: it builds an in-memory stream and changes no state.')]
    param([string]$Text)
    [System.IO.MemoryStream]::new([System.Text.Encoding]::UTF8.GetBytes($Text))
}
