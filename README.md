# KYC MCP Server

A **Model Context Protocol (MCP)** server for KYC (Know Your Customer) customer verification, built with **.NET 10 Web API**. Exposes three compliance tools — document validation, identity verification, and risk assessment — over the MCP JSON-RPC 2.0 transport, making it ready to plug directly into Claude Desktop, Claude API tool use, or any MCP-compatible AI system.

---

## Features

| Tool | Description |
|------|-------------|
| `validate_document` | Checks document format, expiry, and blacklist status |
| `verify_identity` | Screens customers against PEP, sanctions, and watchlists |
| `assess_risk` | Produces an AML risk score with recommended actions |

**Production-ready qualities:**
- Structured logging via **Serilog** (console + rolling file)
- Global error handling mapped to JSON-RPC 2.0 error codes
- `IHttpClientFactory` with per-API timeout and API-key injection
- Strongly-typed configuration with environment-variable override
- Swagger / OpenAPI UI in development
- CORS pre-configured for Claude Desktop integration
- 20+ xUnit unit tests with Moq and FluentAssertions

---

## Project Structure

```
kyc-mcp-server/
├── KYCMcpServer.sln
├── .env.example               # All supported environment variables
├── .gitignore
├── src/
│   ├── KYCMcpServer.Core/     # Domain models & interfaces (no dependencies)
│   │   ├── Models/
│   │   │   ├── Mcp/           # McpRequest, McpResponse, McpTool
│   │   │   └── Kyc/           # DocumentValidation, IdentityVerification, RiskAssessment
│   │   └── Interfaces/        # IDocumentValidationService, IIdentityVerificationService …
│   │
│   ├── KYCMcpServer.Services/ # Business logic implementations
│   │   ├── DocumentValidationService.cs
│   │   ├── IdentityVerificationService.cs
│   │   ├── RiskAssessmentService.cs
│   │   ├── McpToolHandler.cs  # Routes MCP tool calls to services
│   │   └── BankingApiClient.cs
│   │
│   └── KYCMcpServer.Api/      # ASP.NET Core Web API host
│       ├── Controllers/McpController.cs
│       ├── Middleware/ErrorHandlingMiddleware.cs
│       ├── Configuration/AppSettings.cs
│       └── Program.cs
│
└── tests/
    └── KYCMcpServer.Tests/    # xUnit unit tests
```

---

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 10.0+ |
| (Optional) Real banking API keys | — |

Install .NET: https://dotnet.microsoft.com/download

---

## Setup

### 1. Clone and configure

```bash
git clone https://github.com/your-org/kyc-mcp-server.git
cd kyc-mcp-server
cp .env.example .env
```

Edit `.env` and fill in your API keys:

```env
BANKING_API_KEY=your_key_here
IDENTITY_VERIFICATION_API_KEY=your_key_here
DOCUMENT_VERIFICATION_API_KEY=your_key_here
AML_API_KEY=your_key_here
```

> **Note:** Without real API keys the server still runs. Document/identity checks will throw `BankingApiException` and return a "service unavailable" note — which is the correct fallback behaviour for missing integrations.

### 2. Restore dependencies

```bash
dotnet restore
```

### 3. Run the server

```bash
dotnet run --project src/KYCMcpServer.Api
```

The server starts on `http://localhost:5000` (or the port in `ASPNETCORE_URLS`).

- **Swagger UI:** http://localhost:5000/swagger
- **Health check:** http://localhost:5000/health
- **Tool list (REST):** http://localhost:5000/tools

### 4. Run tests

```bash
dotnet test
```

---

## MCP Protocol Usage

### Initialize

```json
POST /
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2024-11-05",
    "clientInfo": { "name": "claude-desktop", "version": "1.0" }
  }
}
```

### List tools

```json
POST /
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/list"
}
```

### Call a tool

#### validate_document

```json
POST /
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "validate_document",
    "arguments": {
      "customer_id": "CUST001",
      "document_type": "Passport",
      "document_number": "AB123456",
      "country_code": "US",
      "expiry_date": "2030-01-15"
    }
  }
}
```

#### verify_identity

```json
POST /
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "tools/call",
  "params": {
    "name": "verify_identity",
    "arguments": {
      "customer_id": "CUST001",
      "first_name": "Jane",
      "last_name": "Doe",
      "date_of_birth": "1988-04-22",
      "nationality_code": "GB"
    }
  }
}
```

#### assess_risk

```json
POST /
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "tools/call",
  "params": {
    "name": "assess_risk",
    "arguments": {
      "customer_id": "CUST001",
      "customer_type": "Individual",
      "country_of_residence": "US",
      "country_of_origin": "US",
      "source_of_funds": "Salary",
      "expected_annual_transaction_volume": 50000
    }
  }
}
```

---

## Claude Desktop Integration

Add the server to your Claude Desktop `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "kyc-server": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/kyc-mcp-server/src/KYCMcpServer.Api"],
      "env": {
        "BANKING_API_KEY": "your_key",
        "ASPNETCORE_ENVIRONMENT": "Production"
      }
    }
  }
}
```

---

## Integrating a Real Banking API

The `BankingApiClient` in [src/KYCMcpServer.Services/BankingApiClient.cs](src/KYCMcpServer.Services/BankingApiClient.cs) implements `IBankingApiClient`. To swap in a real provider:

1. Create a new class implementing `IBankingApiClient` (e.g. `JumioApiClient`)
2. Register it in `Program.cs`:
   ```csharp
   builder.Services.AddHttpClient<IBankingApiClient, JumioApiClient>(...);
   ```
3. Set the matching `*_API_BASE_URL` and `*_API_KEY` environment variables

No other changes required — all services depend on the interface, not the implementation.

---

## Environment Variables Reference

| Variable | Default | Description |
|----------|---------|-------------|
| `BANKING_API_KEY` | — | Primary banking/compliance API key |
| `BANKING_API_BASE_URL` | `https://api.banking.example.com/` | Base URL for banking API |
| `IDENTITY_VERIFICATION_API_KEY` | — | Identity verification provider key |
| `IDENTITY_VERIFICATION_API_BASE_URL` | `https://api.identity.example.com/` | Identity API base URL |
| `DOCUMENT_VERIFICATION_API_KEY` | — | Document verification provider key |
| `DOCUMENT_VERIFICATION_API_BASE_URL` | `https://api.documents.example.com/` | Document API base URL |
| `AML_API_KEY` | — | AML/sanctions screening provider key |
| `AML_API_BASE_URL` | `https://api.aml.example.com/` | AML API base URL |
| `REQUEST_TIMEOUT_SECONDS` | `30` | HTTP client timeout |
| `ENABLE_DETAILED_ERRORS` | `false` | Include stack traces in error responses |
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Development` / `Staging` / `Production` |
| `ASPNETCORE_URLS` | `http://localhost:5000` | Server listen URL |

---

## License

MIT
