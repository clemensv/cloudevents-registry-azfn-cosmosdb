:: Generated by: https://openapi-generator.tech
::

@echo off

dotnet restore src\xRegistry.Types.AzureFunctions
dotnet build src\xRegistry.Types.AzureFunctions
echo Now, run the following to start the project: dotnet run -p src\xRegistry.Types.AzureFunctions\xRegistry.Types.AzureFunctions.csproj --launch-profile web.
echo.