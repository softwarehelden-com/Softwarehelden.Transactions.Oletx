param (
    [Parameter(Mandatory=$true)]
    [string]$Version
)

& dotnet pack .\Softwarehelden.Transactions.Oletx.sln `
  -c Release `
  -o .\_pub `
  -p:Version=$Version `
  -p:SignAssembly=True `
  -p:AssemblyOriginatorKeyFile=$env:SIGNING_ASSEMBLY_KEY_FILE `
  -p:SigningCertificateThumbprint=$env:SIGNING_CERTIFICATE_THUMBPRINT `
  -p:SigningTimestampUrl=$env:SIGNING_TIMESTAMP_URL