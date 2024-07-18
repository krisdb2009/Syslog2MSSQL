FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
COPY . /build
WORKDIR /build
RUN dotnet restore
RUN dotnet publish Syslog2MSSQL -c Release -o /Syslog2MSSQL --no-restore
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /Syslog2MSSQL
COPY --from=build /Syslog2MSSQL .
ENV S2M_CONNECTIONSTRING="User ID=logs;Password=logs;Initial Catalog=logs;Server=logs;TrustServerCertificate=true;"
ENTRYPOINT ./Syslog2MSSQL