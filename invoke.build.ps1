# Install PowerShell Core:
#   https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell
# Install Invoke-Build:
#   https://github.com/nightroman/Invoke-Build
# (Optional) Add "Set-Alias ib Invoke-Build" to your PS profile.
# At a PS prompt, run any build task (optionally use "ib" alias):
#   Invoke-Build build
#   Invoke-Build ?  # this lists available tasks

param (
    $NuGetApiPushKey = ( property NuGetApiPushKey 'MISSING' ),
    $LocalPackageDir = ( property LocalPackageDir 'C:\code\LocalPackages' )
)

$baseProjectName = "BinaryToTextEncoding"
$basePackageName = "Zanaptak.$baseProjectName"

task . Build

task Clean {
    exec { dotnet clean .\src -c Release }
}

task Build {
    exec { dotnet build .\src -c Release }
}

task TestJs {
    Set-Location .\test
    if ( -not ( Test-Path node_modules ) ) { exec { npm install } }
    remove bld
    exec { npm test }
}

task TestNet Clean, Build, {
    Set-Location .\test
    exec { dotnet run -c Release }
}

task Test TestJs, TestNet

task Benchmark Clean, Build, {
    Set-Location .\benchmark
    exec { dotnet run -c Release }
}

task Pack Clean, Build, {
    exec { dotnet pack .\src -c Release }
}

task PackInternal Clean, Build, GetVersion, {
    $yearStart = Get-Date -Year ( ( Get-Date ).Year ) -Month 1 -Day 1 -Hour 0 -Minute 0 -Second 0 -Millisecond 0
    $now = Get-Date
    $buildSuffix = [ int ] ( ( $now - $yearStart ).TotalSeconds )
    $internalVersion = "$Version.$buildSuffix"
    exec { dotnet pack .\src -c Release -p:PackageVersion=$internalVersion }
    $filename = "$basePackageName.$internalVersion.nupkg"
    Copy-Item .\src\bin\Release\$filename $LocalPackageDir
    Write-Build Green "Copied $filename to $LocalPackageDir"
}

function UpdateProjectFile(
    [ string ] $Filename ,
    [ string ] $XPath ,
    [ string ] $Value
) {

    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load( $Filename )

    $node = $xml.SelectSingleNode( $XPath )
    if ( -not ( $node ) ) { throw "xpath not found" }
    $node.InnerText = $Value

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.OmitXmlDeclaration = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding( $true )

    $writer = [ System.Xml.XmlWriter ]::Create( $Filename , $settings )
    try {
        $xml.Save( $writer )
    } finally {
        $writer.Dispose()
    }
}

task IncrementMinor GetVersion, {
    if ( $Version -match "^(\d+)\.(\d+)\.(\d+)$" ) {
        $projectFile = "$BuildRoot\src\$baseProjectName.fsproj"
        $major = $Matches[ 1 ]
        $minor = $Matches[ 2 ]
        $patch = $Matches[ 3 ]
        $newMinor = ( [ int ] $minor ) + 1
        $newVersion = "$major.$newMinor.0"
        UpdateProjectFile $projectFile '/Project/PropertyGroup/Version' $newVersion
        Write-Build Green "Updated version to $newVersion"
    }
    else {
        throw "invalid version: $Version"
    }
}

task IncrementPatch GetVersion, {
    if ( $Version -match "^(\d+)\.(\d+)\.(\d+)$" ) {
        $projectFile = "$BuildRoot\src\$baseProjectName.fsproj"
        $major = $Matches[ 1 ]
        $minor = $Matches[ 2 ]
        $patch = $Matches[ 3 ]
        $newPatch = ( [ int ] $patch ) + 1
        $newVersion = "$major.$minor.$newPatch"
        UpdateProjectFile $projectFile '/Project/PropertyGroup/Version' $newVersion
        Write-Build Green "Updated version to $newVersion"
    }
    else {
        throw "invalid version: $Version"
    }
}

task GetVersion {
    $script:Version = Select-Xml -Path ".\src\$baseProjectName.fsproj" -XPath /Project/PropertyGroup/Version | % { $_.Node.InnerXml.Trim() }
}

task UploadNuGet EnsureCommitted, GetVersion, {
    if ( $NuGetApiPushKey -eq "MISSING" ) { throw "NuGet key not provided" }
    Set-Location ./src/bin/Release
    $filename = "$basePackageName.$Version.nupkg"
    if ( -not ( Test-Path $filename ) ) { throw "nupkg file not found" }
    $lastHour = ( Get-Date ).AddHours( -1 )
    if ( ( Get-ChildItem $filename ).LastWriteTime -lt $lastHour ) { throw "nupkg file too old" }
    exec { dotnet nuget push $filename -k $NuGetApiPushKey -s https://api.nuget.org/v3/index.json }
}

task EnsureCommitted {
    $gitoutput = exec { git status -s -uall }
    if ( $gitoutput ) { throw "uncommitted changes exist in working directory" }
}
