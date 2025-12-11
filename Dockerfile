# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["CopyTrading/CopyTrading.csproj", "CopyTrading/"]
COPY ["CopyTrading.Models/CopyTrading.Models.csproj", "CopyTrading.Models/"]
RUN dotnet restore "CopyTrading/CopyTrading.csproj"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src/CopyTrading"
RUN dotnet build "CopyTrading.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "CopyTrading.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create directory for SQLite databases
RUN mkdir -p /app/data/sqlite

# Expose ports
EXPOSE 5000
EXPOSE 5001

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000;https://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "CopyTrading.dll"]
