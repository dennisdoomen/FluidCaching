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

task default -depends Clean, ExtractVersionsFromGit, ApplyPackageVersioning, RestoreNugetPackages, Compile, RunTests, BuildPackage, PublishToMyget

task Clean {	
    TeamCity-Block "Clean" {
		Get-ChildItem $PackageDirectory *.nupkg | ForEach { Remove-Item $_.FullName }
	    exec { msbuild $SlnFile /p:Configuration=Release /t:Clean $logger}
    }
}

task ExtractVersionsFromGit {

        $json = . "$GitVersionExe" "$BaseDirectory" 

        if ($LASTEXITCODE -eq 0) {
            $version = (ConvertFrom-Json ($json -join "`n"));

            TeamCity-SetBuildNumber $version.FullSemVer;

            $script:AssemblyVersion = $version.ClassicVersion;
            $script:InfoVersion = $version.InformationalVersion;
            $script:NuGetVersion = $version.NuGetVersion;
        }
        else {
            Write-Output $json -join "`n";
        }
}

task ApplyPackageVersioning -depends ExtractVersionsFromGit {
    TeamCity-Block "Updating package version to $script:NuGetVersion" {   
	
		$fullName = "$SrcDirectory\.nuspec"

	    Set-ItemProperty -Path $fullName -Name IsReadOnly -Value $false
		
	    $content = Get-Content $fullName
	    $content = $content -replace '<version>.+</version>', ('<version>' + "$script:NuGetVersion" + '</version>')
	    Set-Content -Path $fullName $content
	}
}

task RestoreNugetPackages {

	& $Nuget restore "$BaseDirectory\FluidCaching.sln"  
	& $Nuget install "$BaseDirectory\Build\packages.config" -OutputDirectory "$BaseDirectory\Packages" -ConfigFile "$BaseDirectory\NuGet.Config"
}

task Compile {
    TeamCity-Block "Compiling" {  
       
	    exec { msbuild /v:m /p:Platform="Any CPU" $SlnFile /p:Configuration=Release /p:SourceAnalysisTreatErrorsAsWarnings=false /t:Rebuild $logger}
    }
}

task RunTests -depends Compile -Description "Running all unit tests." {
    $xunitRunner = "$BaseDirectory\Packages\xunit.runner.console.2.0.0\tools\xunit.console.exe"
	
    if (!(Test-Path $ArtifactsDirectory)) {
		New-Item $ArtifactsDirectory -Type Directory
	}

	exec { . $xunitRunner "$BaseDirectory\Tests\FluidCaching.Specs\bin\Release\FluidCaching.Specs.dll" -html "$ArtifactsDirectory\$project.html"  }
}

    
task BuildPackage {
    TeamCity-Block "Building NuGet Package" {  
		if (!(Test-Path "$ArtifactsDirectory\")) {
			New-Item $ArtifactsDirectory -ItemType Directory
		}
        
        & "$ArtifactsDirectory\MergeCSharpFiles.exe" "$SrcDirectory\FluidCaching" *.cs "$ArtifactsDirectory\FluidCaching.cs"
	
		& $Nuget pack "$SrcDirectory\.nuspec" -o "$ArtifactsDirectory\" 
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


