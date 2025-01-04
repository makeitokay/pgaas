### build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY . ./
RUN dotnet restore "./pgaas.backend/pgaas.backend.csproj"
RUN dotnet build "./pgaas.backend/pgaas.backend.csproj"

### publish
FROM build as publish
RUN dotnet publish pgaas.backend -c Release -o out/pgaas.backend

### pgaas.backend
FROM mcr.microsoft.com/dotnet/aspnet:9.0 as pgaas.backend
WORKDIR /app
COPY --from=publish /app/out/pgaas.backend .

EXPOSE 80
ENV ASPNETCORE_URLS=http://*:8080

ENTRYPOINT ["dotnet", "pgaas.backend.dll"]