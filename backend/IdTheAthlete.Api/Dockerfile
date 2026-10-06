FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
# No ASPNETCORE_URLS here: it would override the PORT a host like Render
# assigns. The aspnet base image already defaults to port 8080 via
# ASPNETCORE_HTTP_PORTS, which PORT or ASPNETCORE_URLS can override.
ENTRYPOINT ["dotnet", "IdTheAthlete.Api.dll"]
