# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
WORKDIR /src

COPY global.json ./
COPY ChildAllowanceManager/ChildAllowanceManager.csproj ChildAllowanceManager/
COPY ChildAllowanceManager.Common/ChildAllowanceManager.Common.csproj ChildAllowanceManager.Common/
RUN dotnet restore ChildAllowanceManager/ChildAllowanceManager.csproj

COPY ChildAllowanceManager/ ChildAllowanceManager/
COPY ChildAllowanceManager.Common/ ChildAllowanceManager.Common/
RUN dotnet publish ChildAllowanceManager/ChildAllowanceManager.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine-extra AS final
# Npgsql probes for GSSAPI at connection time, even for password authentication.
RUN apk add --no-cache krb5-libs

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "ChildAllowanceManager.dll"]
