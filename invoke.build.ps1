# Install PowerShell Core:
#   https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell
# Install Invoke-Build:
#   https://github.com/nightroman/Invoke-Build
# (Optional) Add "Set-Alias ib Invoke-Build" to your PS profile.
# At a PS prompt, run any build task (optionally use "ib" alias):
#   Invoke-Build build
#   Invoke-Build ?  # this lists available tasks

param (
    $NuGetApiPushKey = ( property NuGetApiPushKey 'MISSING' ) ,
    $LocalPackageDir = ( property LocalPackageDir 'C:/code/LocalPackages' ) ,
    $Configuration = "Release"
)

[ System.Environment ]::CurrentDirectory = $BuildRoot

$baseProjectName = "BinaryToTextEncoding"
$basePackageName = "Zanaptak.$baseProjectName"
$mainProjectFilePath = "src/$baseProjectName.fsproj"

function trimLeadingZero {
    param ( $item )
    $item = $item.TrimStart( '0' )
    if ( $item -eq "" ) { "0" } else { $item }
}

function combinePrefixSuffix {
    param ( $prefix , $suffix )
    "$prefix-$suffix".TrimEnd( '-' )
}

function writeProjectFileProperty {
    param ( $projectFile , $propertyName , $propertyValue )
    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load( $projectFile )

    $nodePath = '/Project/PropertyGroup/' + $propertyName
    $node = $xml.SelectSingleNode( $nodePath )
    $node.InnerText = $propertyValue

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.OmitXmlDeclaration = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding( $false )

    $writer = [ System.Xml.XmlWriter ]::Create( $projectFile , $settings )
    try {
        $xml.Save( $writer )
    } finally {
        $writer.Dispose()
    }
}

function readProjectFileProperty {
    param ( $projectFile , $propertyName )
    $nodePath = '/Project/PropertyGroup/' + $propertyName
    $propertyValue =
        Select-Xml -Path $projectFile -XPath $nodePath |
            Select-Object -First 1 |
            & { process { $_.Node.InnerXml.Trim() } }
    $propertyValue
}

$changelogEntryRegex = '^#+ (\d+(?:\.\d+){2,}(?:-\S+)?) \((\d\d\d\d-\d\d-\d\d)\)'

function changelogTopEntry {
    foreach ( $line in ( Get-Content .\CHANGELOG.md ) ) {
        if ( $line -match $changelogEntryRegex ) {
            return [ PSCustomObject ] @{ Version = $Matches.1 ; Date = $Matches.2 }
        }
    }
}

function changelogHasUnversionedEntry {
    foreach ( $line in ( Get-Content .\CHANGELOG.md ) ) {
        if ( $line -imatch "^#+ .*Changelog" ) { continue } # skip Changelog header
        if ( $line -match $changelogEntryRegex ) { return $false } # found version match first
        if ( $line -match "^#" ) { return $true } # found unversioned entry line
    }
}

task . Build

task Clean {
    exec { dotnet clean ./src -c $Configuration }
}

task Build {
    exec { dotnet build ./src -c $Configuration }
}

task TestJs {
    Set-Location ./test
    if ( -not ( Test-Path node_modules ) ) { exec { npm install } }
    remove bld
    exec { npm test }
}

task TestNet Clean, Build, {
    Set-Location ./test
    exec { dotnet run -c $Configuration }
}

task Test TestJs, TestNet

task Benchmark {
    $script:Configuration = "Release"
} , Clean , Build , {
    Set-Location ./benchmark
    exec { dotnet run -c $Configuration }
}

task ReportProjectFileVersion {
    $actualVersionPrefix = readProjectFileProperty $mainProjectFilePath "VersionPrefix"
    $actualVersionSuffix = readProjectFileProperty $mainProjectFilePath "VersionSuffix"
    $actualFullVersion = combinePrefixSuffix $actualVersionPrefix $actualVersionSuffix
    Write-Build Green "Version: $actualFullVersion"
}

task LoadVersion {
    $script:VersionPrefix = readProjectFileProperty $mainProjectFilePath "VersionPrefix"
    $script:VersionSuffix = readProjectFileProperty $mainProjectFilePath "VersionSuffix"
    $script:FullVersion = combinePrefixSuffix $VersionPrefix $VersionSuffix
}

task Pack {
    $script:Configuration = "Release"
} , Clean , {
    exec { dotnet pack ./src -c $Configuration -p:ContinuousIntegrationBuild=true }
}

task PackInternal {
    $script:Configuration = "Debug"
} , Clean , LoadVersion , {
    $timestamp = ( Get-Date ).ToString( "yyyyMMdd.HHmmssfff" )
    if ( $VersionSuffix ) {
        $internalVersionPrefix = $VersionPrefix
        $internalVersionSuffix = "$VersionSuffix.0.internal.$timestamp"
    }
    else {
        $parseVersion = [ System.Version ] $VersionPrefix
        $newBuild = [ math ]::Max( $parseVersion.Build , 0 )
        $newRevision = [ math ]::Max( $parseVersion.Revision , 0 ) + 1
        $internalVersionPrefix = [ System.Version ]::new( $parseVersion.Major , $parseVersion.Minor , $newBuild , $newRevision ).ToString( 4 )
        $internalVersionSuffix = "0.internal.$timestamp"
    }
    exec { dotnet pack ./src -c $Configuration -p:VersionPrefix=$internalVersionPrefix -p:VersionSuffix=$internalVersionSuffix }
    $internalFullVersion = combinePrefixSuffix $internalVersionPrefix $internalVersionSuffix
    $filename = "$basePackageName.$internalFullVersion.*nupkg"
    Copy-Item ./src/bin/$Configuration/$filename $LocalPackageDir
    Write-Build Green "Copied $filename to $LocalPackageDir"
}

task UploadNuGet {
    $script:Configuration = "Release"
} , EnsureCommitted , LoadVersion , EnsureChangelogApplied , {
    if ( $NuGetApiPushKey -eq "MISSING" ) { throw "NuGet key not provided" }
    Set-Location ./src/bin/$Configuration
    $filename = "$basePackageName.$FullVersion.nupkg"
    if ( -not ( Test-Path $filename ) ) { throw "nupkg file not found" }
    $lastHour = ( Get-Date ).AddHours( -1 )
    if ( ( Get-Item $filename ).LastWriteTime -lt $lastHour ) { throw "nupkg file too old" }
    exec { dotnet nuget push $filename -k $NuGetApiPushKey -s https://api.nuget.org/v3/index.json }
}

task EnsureCommitted {
    $gitoutput = exec { git status -s -uall }
    if ( $gitoutput ) { throw "uncommitted changes exist in working directory" }
}

task EnsureChangelogApplied LoadVersion , {
    if ( changelogHasUnversionedEntry ) { throw "unversioned entry exists in changelog" }
    $changelog = changelogTopEntry
    if ( $changelog.Version -ne $FullVersion ) { throw "mismatched project version ($FullVersion) and changelog version ($($changelog.Version))" }
    if ( $changelog.Date -notmatch "^\d\d\d\d-\d\d-\d\d$" ) { throw "invalid changelog date ($($changelog.Date))" }
}

task UpdateProjectFromChangelog {
    $changelog = changelogTopEntry
    if ( -not $changelog.Version ) { throw "no version found" }

    if ( $changelog.Version -match '-' ) {
        $prefix , $suffix = $changelog.Version -split '-'
    }
    else {
        $prefix , $suffix = $changelog.Version , ""
    }

    writeProjectFileProperty $mainProjectFilePath "VersionPrefix" $prefix
    writeProjectFileProperty $mainProjectFilePath "VersionSuffix" $suffix

    $anchor = ( $changelog.Version -replace '\.','' ) + "-$($changelog.Date)"
    $url = "https://github.com/zanaptak/$baseProjectName/blob/main/CHANGELOG.md#$anchor"

    writeProjectFileProperty $mainProjectFilePath "PackageReleaseNotes" $url

    Write-Build Green "****"
    Write-Build Green "**** Assumed changelog URL (VERIFY): $url"
    Write-Build Green "****"

} , ReportProjectFileVersion
