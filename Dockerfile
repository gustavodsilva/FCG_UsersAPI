# syntax=docker/dockerfile:1

# ---------- Etapa 1: restore (camada cacheada enquanto os .csproj não mudarem) ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

COPY src/FCG.Users.Api/FCG.Users.Api.csproj                      src/FCG.Users.Api/
COPY src/FCG.Users.Domain/FCG.Users.Domain.csproj                src/FCG.Users.Domain/
COPY src/FCG.Users.Infrastructure/FCG.Users.Infrastructure.csproj src/FCG.Users.Infrastructure/
RUN dotnet restore src/FCG.Users.Api/FCG.Users.Api.csproj

# ---------- Etapa 2: build e publish ----------
FROM restore AS publish
COPY src/ src/
RUN dotnet publish src/FCG.Users.Api/FCG.Users.Api.csproj \
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

ENTRYPOINT ["dotnet", "FCG.Users.Api.dll"]
