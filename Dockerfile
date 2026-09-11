# ──────────────────────────────────────────────
# Estágio 1: Build
# ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# 1. Copia os arquivos de projeto isolados para o restore de dependências
COPY src/submodule/FCG.Shared/FCG.Shared.csproj ./src/submodule/FCG.Shared/
COPY src/FCG.NotificationsForwarder/FCG.NotificationsForwarder.csproj ./src/FCG.NotificationsForwarder/

# 2. Executa o restore inicial para baixar o AWSSDK.SQS e demais pacotes NuGet
RUN dotnet restore ./src/FCG.NotificationsForwarder/FCG.NotificationsForwarder.csproj

# 3. Copia todo o restante do código-fonte para dentro do container
COPY src/submodule/FCG.Shared/ ./src/submodule/FCG.Shared/
COPY src/FCG.NotificationsForwarder/ ./src/FCG.NotificationsForwarder/

# 4. Executa o publish SEM a flag --no-restore para garantir a compilação final limpa
RUN dotnet publish ./src/FCG.NotificationsForwarder/FCG.NotificationsForwarder.csproj \
    -c Release \
    -o /app/publish

# ──────────────────────────────────────────────
# Estágio 2: Runtime
# ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# Copia os binários compilados do estágio de build
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FCG.NotificationsForwarder.dll"]
