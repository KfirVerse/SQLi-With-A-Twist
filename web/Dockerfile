# ============================================================================
#  Web image for the MSSQL "SQLi -> RCE" lab  (ASP.NET Core 8, Kestrel).
# ============================================================================

# ---- Build stage -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (better layer caching).
COPY SqliRceLab.csproj ./
RUN dotnet restore SqliRceLab.csproj

# Copy the rest and publish.
COPY . ./
RUN dotnet publish SqliRceLab.csproj -c Release -o /app /p:UseAppHost=false

# ---- Runtime stage ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "SqliRceLab.dll"]
