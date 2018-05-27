properties {
    $BaseDirectory = Resolve-Path ..     
	$SrcDirectory ="$BaseDirectory\Src"
	$TestsDirectory ="$BaseDirectory\Tests"
    $Nuget = "$BaseDirectory\.nuget\NuGet.exe"
	$SlnFile = "$BaseDirectory\FluidCaching.sln"
	$GitVersionExe = "$BaseDirectory\Lib\GitVersion.exe"
	$ArtifactsDirectory = "$BaseDirectory\Artifacts"

	$NuGetPushSource = ""
	
    $MsBuildLoggerPath = ""
	$Branch = ""
	$RunTests = $false
}

task default -depends Clean, ExtractVersionsFromGit, RestoreNugetPackages, Compile, RunTests, BuildPackage, PublishToMyget

task DetermineMsBuildPath -depends RestoreNugetPackages {
	Write-Host "Adding msbuild to the environment path"

	$installationPath = & "$BaseDirectory\Packages\vswhere.2.4.1\tools\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath

	if ($installationPath) {
		$msbuildPath = join-path $installationPath 'MSBuild\15.0\Bin'

		if (test-path $msbuildPath) {
                        Write-Host "msbuild directory set to $msbuildPath"
			$env:path = "$msbuildPath;$env:path"
		}
	}
}

task Clean -depends DetermineMsBuildPath {	
    TeamCity-Block "Clean" {
		Get-ChildItem $PackageDirectory *.nupkg | ForEach { Remove-Item $_.FullName }
	    exec { msbuild $SlnFile /p:Configuration=Release /t:Clean $logger}
    }
}

task ExtractVersionsFromGit {

        $json = . "$GitVersionExe" "$BaseDirectory" 

        if ($LASTEXITCODE -eq 0) {
            $version = (ConvertFrom-Json ($json -join "`n"));

            $script:AssemblyVersion = $version.AssemblySemVer;
            $script:InfoVersion = $version.InformationalVersion;
            $script:NuGetVersion = $version.NuGetVersion;
        }
        else {
            Write-Output $json -join "`n";
        }
}

task RestoreNugetPackages {

	& $Nuget restore "$BaseDirectory\FluidCaching.sln"  
	& $Nuget install "$BaseDirectory\Build\packages.config" -OutputDirectory "$BaseDirectory\Packages" -ConfigFile "$BaseDirectory\NuGet.Config"
}

task Compile -depends DetermineMsBuildPath, ApplyAssemblyVersioning {
    TeamCity-Block "Compiling" {  
       
	    exec { msbuild /v:m /p:Platform="Any CPU" $SlnFile /p:Configuration=Release /p:SourceAnalysisTreatErrorsAsWarnings=false /t:Rebuild $logger}
    }
}

task ApplyAssemblyVersioning -depends ExtractVersionsFromGit {
	Get-ChildItem -Path "$SrcDirectory/FluidCaching" -Filter "AssemblyInfo.cs" -Recurse -Force | foreach-object {

		$content = Get-Content $_.FullName

		if ($script:AssemblyVersion) {
			Write-Output "Updating " $_.FullName "with version" $script:AssemblyVersion
			$content = $content -replace 'AssemblyVersion\("(.+)"\)', ('AssemblyVersion("' + $script:AssemblyVersion + '")')
			$content = $content -replace 'AssemblyFileVersion\("(.+)"\)', ('AssemblyFileVersion("' + $script:AssemblyVersion + '")')
		}

		if ($script:InfoVersion) {
			Write-Output "Updating " $_.FullName "with information version" $script:InfoVersion
			$content = $content -replace 'AssemblyInformationalVersion\("(.+)"\)', ('AssemblyInformationalVersion("' + $script:InfoVersion + '")')
		}

		Set-Content -Path $_.FullName $content
	}
}

task RunTests -depends Compile -Description "Running all unit tests." {
    $xunitRunner = "$BaseDirectory\Packages\xunit.runner.console.2.2.0\tools\xunit.console.exe"
	
    if (!(Test-Path $ArtifactsDirectory)) {
		New-Item $ArtifactsDirectory -Type Directory
	}

	exec { . $xunitRunner "$BaseDirectory\Tests\FluidCaching.Specs\bin\Release\FluidCaching.Specs.dll" -html "$ArtifactsDirectory\$project.html"  }
}

    
task BuildPackage -depends ExtractVersionsFromGit {
    TeamCity-Block "Building NuGet Package" {  
		if (!(Test-Path "$ArtifactsDirectory\")) {
			New-Item $ArtifactsDirectory -ItemType Directory
		}
        
        & "$ArtifactsDirectory\MergeCSharpFiles.exe" "$SrcDirectory\FluidCaching" *.cs "$ArtifactsDirectory\FluidCaching.cs"
	
		& $Nuget pack "$BaseDirectory\Sources.nuspec" -OutputDirectory "$ArtifactsDirectory\" -Version $script:NuGetVersion
		& $Nuget pack "$BaseDirectory\Binaries.nuspec" -OutputDirectory "$ArtifactsDirectory\" -Version $script:NuGetVersion
	}
}

task PublishToMyget -precondition { return $NuGetPushSource -and $env:NuGetApiKey } {
    TeamCity-Block "Publishing NuGet Package to Myget" {  
		$packages = Get-ChildItem $ArtifactsDirectory *.nupkg
		foreach ($package in $packages) {
			& $Nuget push $package.FullName $env:NuGetApiKey -Source "$NuGetPushSource"
		}
	}
}


