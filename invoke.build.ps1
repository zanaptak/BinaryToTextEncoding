# Install PowerShell for your platform:
#   https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell
# Install Invoke-Build:
#   https://github.com/nightroman/Invoke-Build
# (Optional) Add "Set-Alias ib Invoke-Build" to your PS profile.
# At a PS prompt, run any build task (optionally use "ib" alias):
#   Invoke-Build build
#   Invoke-Build ?  # this lists available tasks

task Clean {
  exec { dotnet clean .\src -c Release }
}

task Build {
  exec { dotnet build .\src -c Release }
}

task Pack Clean, Build, {
  exec { dotnet pack .\src -c Release }
}

task TestJs {
  Set-Location .\test
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
  exec { dotnet run -p BinaryToTextEncoding.Benchmark.fsproj -c Release }
}

task . Build
