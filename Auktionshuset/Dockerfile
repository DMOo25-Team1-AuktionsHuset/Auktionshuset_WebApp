FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props ./

COPY Auktionshuset/Auktionshuset.csproj Auktionshuset/
COPY Auktionshuset.Contracts/Auktionshuset.Contracts.csproj Auktionshuset.Contracts/
COPY Auktionshuset.Domain/Auktionshuset.Domain.csproj Auktionshuset.Domain/

RUN dotnet restore Auktionshuset/Auktionshuset.csproj

COPY . .

RUN dotnet publish Auktionshuset/Auktionshuset.csproj \
    --configuration Release \
    --no-restore \
    --output /publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

USER root

RUN apk add --no-cache \
    curl \
    icu-libs \
    icu-data-full \
    tzdata

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

WORKDIR /app
COPY --from=build /publish .

USER app

ENTRYPOINT ["dotnet", "Auktionshuset.dll"]