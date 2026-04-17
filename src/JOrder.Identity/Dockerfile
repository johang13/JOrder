FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/JOrder.Identity/JOrder.Identity.csproj", "src/JOrder.Identity/"]
COPY ["src/JOrder.Common/JOrder.Common.csproj", "src/JOrder.Common/"]
RUN dotnet restore "src/JOrder.Identity/JOrder.Identity.csproj"
COPY . .
WORKDIR "/src/src/JOrder.Identity"
RUN dotnet build "./JOrder.Identity.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./JOrder.Identity.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "JOrder.Identity.dll"]
