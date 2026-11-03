# syntax=docker/dockerfile:1

# ---------- Etapa 1: restore (camada cacheada enquanto os .csproj não mudarem) ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

COPY src/FCG_UsersApi/FCG_UsersApi.csproj                      src/FCG_UsersApi/
COPY src/FCG_UsersDomain/FCG_UsersDomain.csproj                src/FCG_UsersDomain/
COPY src/FCG_UsersInfrastructure/FCG_UsersInfrastructure.csproj src/FCG_UsersInfrastructure/
RUN dotnet restore src/FCG_UsersApi/FCG_UsersApi.csproj

# ---------- Etapa 2: build e publish ----------
FROM restore AS publish
COPY src/ src/
RUN dotnet publish src/FCG_UsersApi/FCG_UsersApi.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Etapa 3: imagem final (somente runtime, sem SDK) ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# A imagem do ASP.NET escuta na 8080 (não mais na 80) e roda sem privilégios.
# USER numérico: o Kubernetes só consegue validar runAsNonRoot com UID numérico.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "FCG_UsersApi.dll"]
