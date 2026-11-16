FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/LeaseDesk/LeaseDesk.csproj src/LeaseDesk/
RUN dotnet restore src/LeaseDesk/LeaseDesk.csproj
COPY src/LeaseDesk/ src/LeaseDesk/
RUN dotnet publish src/LeaseDesk/LeaseDesk.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
COPY docker/entrypoint.sh /entrypoint.sh
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh \
    && mkdir -p /app/data && chown -R app:app /app /app/data
USER app
ENV ConnectionStrings__LeaseDesk="Data Source=/app/data/leases.db"
EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]
