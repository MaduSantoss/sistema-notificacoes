FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Shared/ src/Shared/
COPY src/Consumidor/ src/Consumidor/
RUN dotnet publish src/Consumidor/Consumidor.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Consumidor.dll"]