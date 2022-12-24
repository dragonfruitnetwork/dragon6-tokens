FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["DragonFruit.Six.TokenRotator.Host/DragonFruit.Six.TokenRotator.Host.csproj", "DragonFruit.Six.TokenRotator.Host/"]
RUN dotnet restore "DragonFruit.Six.TokenRotator.Host/DragonFruit.Six.TokenRotator.Host.csproj"
COPY . .
WORKDIR "/src/DragonFruit.Six.TokenRotator.Host"
RUN dotnet build "DragonFruit.Six.TokenRotator.Host.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DragonFruit.Six.TokenRotator.Host.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DragonFruit.Six.TokenRotator.Host.dll"]
