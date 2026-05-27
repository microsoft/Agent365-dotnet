FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY src/Directory.Build.props src/
COPY src/Directory.Packages.props src/
COPY src/global.json src/
COPY src/Sidecar/Microsoft.Agents.A365.Sidecar/Microsoft.Agents.A365.Sidecar.csproj src/Sidecar/Microsoft.Agents.A365.Sidecar/
COPY src/Observability/Runtime/Microsoft.Agents.A365.Observability.Runtime.csproj src/Observability/Runtime/
COPY src/Tooling/Core/Microsoft.Agents.A365.Tooling.csproj src/Tooling/Core/
COPY src/Notification/Microsoft.Agents.A365.Notifications/Microsoft.Agents.A365.Notifications.csproj src/Notification/Microsoft.Agents.A365.Notifications/
COPY src/Runtime/Core/Microsoft.Agents.A365.Runtime.csproj src/Runtime/Core/

# Restore
RUN dotnet restore src/Sidecar/Microsoft.Agents.A365.Sidecar/Microsoft.Agents.A365.Sidecar.csproj

# Copy everything and build
COPY . .
RUN dotnet publish src/Sidecar/Microsoft.Agents.A365.Sidecar/Microsoft.Agents.A365.Sidecar.csproj \
    -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 4317 4318 5365
ENTRYPOINT ["dotnet", "Microsoft.Agents.A365.Sidecar.dll"]
