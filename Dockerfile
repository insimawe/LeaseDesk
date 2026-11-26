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
COPY docker/local.conf /etc/fonts/conf.d/99-leasedesk-aliases.conf
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        wget \
        ca-certificates \
        util-linux \
        fontconfig \
        fonts-liberation \
        fonts-crosextra-carlito \
        fonts-crosextra-caladea \
    && wget -q -O /tmp/chrome.deb https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb \
    && apt-get install -y --no-install-recommends /tmp/chrome.deb \
    && rm /tmp/chrome.deb \
    && fc-cache -f \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/data && chown -R app:app /app /app/data
ENV ConnectionStrings__LeaseDesk="Data Source=/app/data/leases.db"
EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]
