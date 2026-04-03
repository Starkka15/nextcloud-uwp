# Downloads .NET Native 2.1 ARM dependency packages from NuGet
# Places appx files in Z:\W10M-Dependencies\arm\

$dest = 'Z:\W10M-Dependencies\arm'
$tmp  = "$env:TEMP\native21-dl"
New-Item $tmp -ItemType Directory -Force | Out-Null

$packages = @(
    @{ id = "runtime.win10-arm.microsoft.net.native.compiler";      appxPath = "tools/Runtime/arm";                         nameHint = "Runtime" },
    @{ id = "runtime.win10-arm.microsoft.net.native.sharedlibrary"; appxPath = "tools/SharedLibrary/ret/Native";            nameHint = "Framework" },
    @{ id = "runtime.win10-arm.microsoft.net.uwpcoreruntimesdk";    appxPath = "tools/Appx";                                nameHint = "CoreRuntime" }
)

foreach ($pkg in $packages) {
    $id      = $pkg.id
    $idLower = $id.ToLower()
    $hint    = $pkg.nameHint

    Write-Host "Resolving $id ..."
    try {
        $index = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$idLower/index.json" -UseBasicParsing
    } catch {
        Write-Error "Failed to resolve versions for $id`: $_"
        continue
    }

    # Find highest 2.1.x version
    $ver = $index.versions | Where-Object { $_ -like "2.1.*" } | Select-Object -Last 1
    if (-not $ver) {
        Write-Error "No 2.1.x version found for $id"
        continue
    }
    Write-Host "  Found version $ver"

    $nupkg = "$tmp\$idLower.$ver.zip"
    $extract = "$tmp\$idLower"

    try {
        Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$idLower/$ver/$idLower.$ver.nupkg" `
            -OutFile $nupkg -UseBasicParsing
    } catch {
        Write-Error "Failed to download $id $ver`: $_"
        continue
    }

    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive $nupkg -DestinationPath $extract -Force

    $appx = Get-ChildItem $extract -Recurse -Filter "*.appx" |
            Where-Object { $_.Name -notlike "*Debug*" -and $_.Name -notlike "*arm64*" } |
            Select-Object -First 1

    if (-not $appx) {
        Write-Error "No release ARM appx found in $id $ver"
        continue
    }

    $destFile = "$dest\$($appx.Name)"
    Copy-Item $appx.FullName -Destination $destFile -Force
    Write-Host "  Copied: $($appx.Name)"
}

Remove-Item $tmp -Recurse -Force
Write-Host ""
Write-Host "Done. Files in $dest`:"
Get-ChildItem $dest | Select-Object Name | Format-Table -HideTableHeaders
