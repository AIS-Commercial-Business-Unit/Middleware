FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./dotnet-file-processing/dotnet-file-processing.csproj
RUN dotnet publish ./dotnet-file-processing/dotnet-file-processing.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update         && apt-get install -y --no-install-recommends wget         && rm -rf /var/lib/apt/lists/*         && groupadd -r appuser && useradd -r -g appuser -d /home/appuser -m appuser
ENV ASPNETCORE_URLS=http://+:8187
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/data/renewals/inbound /app/data/renewals/processed /app/data/renewals/error         && chown -R appuser:appuser /app
USER appuser
EXPOSE 8187
ENTRYPOINT ["dotnet", "dotnet-file-processing.dll"]
