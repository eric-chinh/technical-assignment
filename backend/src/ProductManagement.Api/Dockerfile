FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ProductManagement.slnx .
COPY src/ProductManagement.Domain/ProductManagement.Domain.csproj src/ProductManagement.Domain/
COPY src/ProductManagement.Application/ProductManagement.Application.csproj src/ProductManagement.Application/
COPY src/ProductManagement.Infrastructure/ProductManagement.Infrastructure.csproj src/ProductManagement.Infrastructure/
COPY src/ProductManagement.Api/ProductManagement.Api.csproj src/ProductManagement.Api/
RUN dotnet restore src/ProductManagement.Api/ProductManagement.Api.csproj

COPY src/ src/
RUN dotnet publish src/ProductManagement.Api/ProductManagement.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProductManagement.Api.dll"]
