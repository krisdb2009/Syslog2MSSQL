# Syslog2MSSQL

A lightweight .NET 8 service that listens for Syslog over UDP (default port **514**), parses messages (RFC‑5424, RFC‑3164, key–value formats, and plain text), and writes them to a Microsoft SQL Server table.

## Features

- **UDP syslog listener** with large receive buffer (default **1 GB**) for high‑throughput scenarios.
- **Parallel stream parser** with batching and multi‑threading.
- **Message variants supported**:
  - RFC‑5424 (structured data)
  - RFC‑3164 (BSD)
  - Key–value pairs (e.g., firewall logs)
  - Plain text fallback
- **Value extraction** for IPv4/IPv6 addresses and consolidation into a data dictionary.
- **Docker support** for easy containerized deployment.

## Project layout

```
Syslog2MSSQL/
  Common/                  # Time helpers, batching queue, observable, parallel processor
  Model/                   # Parsed message model & serializer
  Parsing/                 # Parsers, extractors, helpers
  Udp/                     # UDP listener/sender and pipeline
  Program.cs               # Entry point — wires the pipeline to SQL insert
Dockerfile                 # Multi-stage build for runtime image
.github/workflows/deploy.yaml  # GitHub Actions example to push image
LICENSE.txt                # MIT License
```

## Requirements

- **.NET SDK** 8.0 (for local build)
- **SQL Server** reachable from the service
- Table **[logs]** in the target database (see schema below)

## Build & Run (local)

```bash
# Restore & build
 dotnet restore
 dotnet publish Syslog2MSSQL -c Release -o ./out

# Set connection string (example)
 export S2M_CONNECTIONSTRING='User ID=logs;Password=logs;Initial Catalog=logs;Server=your-sql-host;TrustServerCertificate=true;'

# Run
 ./out/Syslog2MSSQL
```

> The application prints a banner with version on start, then `*` per parsed message and `#` after a successful insert.

## Docker

A multi-stage Dockerfile is provided.

```bash
# Build image
 docker build -t syslog2mssql:latest .

# Run container (listens on UDP 514)
 docker run -d \
  --name syslog2mssql \
  -e S2M_CONNECTIONSTRING='User ID=logs;Password=logs;Initial Catalog=logs;Server=your-sql-host;TrustServerCertificate=true;' \
  -p 514:514/udp \
  syslog2mssql:latest
```

### GitHub Container Registry (optional)
The provided workflow pushes `ghcr.io/krisdb2009/syslogmssql:latest`. Update owner/name as needed if you fork.

## Configuration

- **Environment variable** `S2M_CONNECTIONSTRING` — ADO.NET connection string used by `Microsoft.Data.SqlClient`.
- **UDP settings** (defaults shown):
  - Port: `514`
  - Receive buffer: `1 * 1024 * 1024 * 1024` bytes (1 GB)
- **Parser threading**:
  - Batch size: `100`
  - Thread count: `Environment.ProcessorCount`

You can change defaults by editing `Syslog2MSSQL/Udp/SyslogUdpListener.cs` and `Syslog2MSSQL/Parsing/SyslogStreamParser.cs`.

## Database schema

Create a simple table to store parsed messages:

```sql
CREATE TABLE [dbo].[logs] (
  [id] INT IDENTITY(1,1) PRIMARY KEY,
  [time]      DATETIME2     NULL,
  [host]      NVARCHAR(256) NULL,
  [severity]  NVARCHAR(32)  NULL,
  [facility]  NVARCHAR(32)  NULL,
  [application] NVARCHAR(256) NULL,
  [process]   NVARCHAR(256) NULL,
  [message]   NVARCHAR(MAX) NULL
);
```

> Insert logic is implemented in `Program.cs` using parameterized `SqlCommand` to write one row per parsed message.

## How it works

1. **SyslogUdpListener** binds to `IPAddress.Any` and the configured port, receiving UDP datagrams and broadcasting `RawSyslogMessage` objects.
2. **SyslogStreamParser** subscribes to the listener and parses messages concurrently into `ParsedSyslogMessage` instances using the configured variant parsers.
3. **Program.cs** subscribes to `ItemProcessed` and performs an INSERT into `[logs]` using `S2M_CONNECTIONSTRING`.

### Supported formats (details)
- **RFC‑5424**: Reads header fields (timestamp, host, appName, procId, msgId) and structured data blocks like `[elem key="value"]`. Message BOM is stripped if present.
- **RFC‑3164**: Parses month/day/time prefix, hostname, optional process tag, and the rest of the line as message.
- **Key–value**: Detects `key=value` sequences (malformed values handled best‑effort), extracts known header hints like `device_id`, `date`, `time`.
- **Plain text**: Fallback to treat remaining payload after the `<pri>` prefix as message.

## License

MIT — see `LICENSE.txt`.

## Credits

Parsing & helper components are adapted under Microsoft copyright notices; this project wires them into a SQL ingestion pipeline.

