FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/RelayWorks.Migrations/RelayWorks.Migrations.csproj -c Release -o /app/publish --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RelayWorks.Migrations.dll"]
