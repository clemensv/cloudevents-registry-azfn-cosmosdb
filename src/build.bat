:: Generated by: https://openapi-generator.tech
::

@echo off

dotnet restore src\xRegistry.Types.AspNet
dotnet build src\xRegistry.Types.AspNet
echo Now, run the following to start the project: dotnet run -p src\xRegistry.Types.AspNet\xRegistry.Types.AspNet.csproj --launch-profile web.
echo.
