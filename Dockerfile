# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["PastryManager/PastryManager.Api.csproj", "PastryManager/"]
COPY ["PastryManager.Application/PastryManager.Application.csproj", "PastryManager.Application/"]
COPY ["PastryManager.Infrastructure/PastryManager.Infrastructure.csproj", "PastryManager.Infrastructure/"]
COPY ["PastryManager.Domain/PastryManager.Domain.csproj", "PastryManager.Domain/"]
RUN dotnet restore "PastryManager/PastryManager.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/PastryManager"
RUN dotnet build "PastryManager.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "PastryManager.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy published files
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "PastryManager.Api.dll"]
