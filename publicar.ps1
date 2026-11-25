# Deja en una carpeta todo lo que hay que llevar al equipo del cliente para usar card-lint sin conexión:
# el ejecutable, Cppcheck (solo la línea de comandos), PMD y las licencias.
#
#   .\publicar.ps1                      # autocontenido: el equipo de destino no necesita .NET
#   .\publicar.ps1 -Autocontenido:$false   # necesita .NET 10 instalado en el destino
#
# Los paquetes de NuGet que haga falta descargar van a .paquetes\, dentro del repositorio.

param(
    [string]$Salida = (Join-Path $PSScriptRoot 'publicacion'),
    [bool]$Autocontenido = $true
)

$ErrorActionPreference = 'Stop'
$herramientas = Join-Path $PSScriptRoot 'herramientas'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.paquetes'

$cppcheck = Get-ChildItem $herramientas -Recurse -Filter 'cppcheck.exe' | Select-Object -First 1
$pmd = Get-ChildItem $herramientas -Directory -Filter 'pmd-bin-*' | Sort-Object Name | Select-Object -Last 1
if (-not $cppcheck -or -not $pmd) {
    throw "Faltan Cppcheck o PMD en $herramientas."
}

if (Test-Path $Salida) {
    Remove-Item $Salida -Recurse -Force
}

# 1. card-lint
$proyecto = Join-Path $PSScriptRoot 'src\CardLint.Cli\CardLint.Cli.csproj'
$opciones = @('publish', $proyecto, '-c', 'Release', '-r', 'win-x64', '-o', $Salida, '-p:PublishSingleFile=true')
$opciones += if ($Autocontenido) { '--self-contained' } else { '--no-self-contained' }
& dotnet @opciones
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish terminó con el código $LASTEXITCODE."
}

# 2. Cppcheck: el ejecutable, su biblioteca, su configuración y sus licencias; nada de la interfaz gráfica.
$origen = $cppcheck.DirectoryName
$destino = Join-Path $Salida 'herramientas\cppcheck'
New-Item -ItemType Directory -Force $destino | Out-Null
Copy-Item (Join-Path $origen 'cppcheck.exe'), (Join-Path $origen 'cppcheck-core.dll') $destino
Copy-Item (Join-Path $origen 'cfg') $destino -Recurse
Copy-Item (Join-Path $origen '*.txt') $destino

# 3. PMD, entero.
Copy-Item $pmd.FullName (Join-Path $Salida 'herramientas') -Recurse

# 4. Licencias.
$licencias = Join-Path $Salida 'licencias'
New-Item -ItemType Directory -Force $licencias | Out-Null
Copy-Item (Join-Path $origen 'GPLv3.txt') (Join-Path $licencias 'cppcheck-GPLv3.txt')
Copy-Item (Join-Path $pmd.FullName 'LICENSE') (Join-Path $licencias "$($pmd.Name)-LICENSE.txt")
Copy-Item (Join-Path $PSScriptRoot 'docs\licencias-herramientas.md') (Join-Path $licencias 'LEEME.md')

Write-Host "Publicado en $Salida"
