FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

ADD . .
ENTRYPOINT ["dotnet", "DragonFruit.Six.TokenRotator.Service.dll"]