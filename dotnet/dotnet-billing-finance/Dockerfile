FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./dotnet-billing-finance/dotnet-billing-finance.csproj
RUN dotnet publish ./dotnet-billing-finance/dotnet-billing-finance.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update         && apt-get install -y --no-install-recommends wget         && rm -rf /var/lib/apt/lists/*         && groupadd -r appuser && useradd -r -g appuser -d /home/appuser -m appuser
ENV ASPNETCORE_URLS=http://+:8185
WORKDIR /app
COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app
USER appuser
EXPOSE 8185
ENTRYPOINT ["dotnet", "dotnet-billing-finance.dll"]
